using EgyptOnline.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace EgyptOnline.Services
{
    /// <summary>
    /// Cloudflare R2-backed CDN service with two-bucket strategy:
    ///   • PUBLIC  bucket  → profile photos        → anonymous read (via R2 public bucket custom domain), permanent URLs
    ///   • PRIVATE bucket  → KYC docs / receipts   → no public access, presigned URLs only
    /// </summary>
    public class R2StorageService : ICDNService
    {
        private readonly IAmazonS3 _s3;
        private readonly string _publicBucket;
        private readonly string _privateBucket;
        private readonly string _publicBaseUrl;
        private readonly ILogger<R2StorageService> _logger;

        /// <inheritdoc/>
        public string PublicBaseUrl => _publicBaseUrl;

        // ── Allowed image magic-byte signatures ───────────────────────────────────
        private static readonly IReadOnlyList<(byte[] Magic, int Offset)> AllowedMagicBytes =
            new List<(byte[], int)>
            {
                (new byte[] { 0xFF, 0xD8, 0xFF }, 0),                                   // JPEG
                (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),   // PNG
                (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, 0),                 // GIF87a
                (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, 0),                 // GIF89a
                (new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0),                             // WEBP (RIFF)
            };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        public R2StorageService(IConfiguration config, ILogger<R2StorageService> logger)
        {
            _logger = logger;

            var endpoint  = config["R2:Endpoint"]          ?? throw new InvalidOperationException("R2:Endpoint is not configured.");
            var accessKey = config["R2:AccessKeyId"]       ?? throw new InvalidOperationException("R2:AccessKeyId is not configured.");
            var secretKey = config["R2:SecretAccessKey"]   ?? throw new InvalidOperationException("R2:SecretAccessKey is not configured.");

            _publicBucket  = config["R2:PublicBucketName"]  ?? "egypt-online-public";
            _privateBucket = config["R2:PrivateBucketName"] ?? "egypt-online-private";
            _publicBaseUrl = (config["R2:PublicBaseUrl"] ?? throw new InvalidOperationException("R2:PublicBaseUrl is not configured.")).TrimEnd('/');

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            };

            _s3 = new AmazonS3Client(accessKey, secretKey, s3Config);

            // Ensure both buckets exist at startup (R2 requires bucket-creation permission; harmless if they exist)
            _ = EnsureBucketExistsAsync(_publicBucket);
            _ = EnsureBucketExistsAsync(_privateBucket);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  PUBLIC API – profile photos
        // ═══════════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "images")
        {
            ValidateImage(fileBytes, fileName);

            fileName = SanitizeFileName(fileName);
            folder   = SanitizeFolder(folder);
            var objectKey = $"{folder}/{fileName}";

            await PutObjectAsync(_publicBucket, objectKey, fileBytes);

            var url = $"{_publicBaseUrl}/{objectKey}";
            _logger.LogInformation("PUBLIC upload: {ObjectKey} → {Url}", objectKey, url);
            return url;
        }

        /// <inheritdoc/>
        public async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return;
            var objectKey = ExtractObjectKey(imageUrl, _publicBaseUrl, _publicBucket);
            await RemoveObjectAsync(_publicBucket, objectKey);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  PRIVATE API – KYC documents & deposit receipts
        // ═══════════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<string> UploadPrivateImageAsync(byte[] fileBytes, string fileName, string folder)
        {
            ValidateImage(fileBytes, fileName);

            fileName = SanitizeFileName(fileName);
            folder   = SanitizeFolder(folder);
            var objectKey = $"{folder}/{fileName}";

            await PutObjectAsync(_privateBucket, objectKey, fileBytes);

            // Return the object key, NOT a public URL — callers must use GetPresignedUrlAsync
            _logger.LogInformation("PRIVATE upload: bucket={Bucket} key={ObjectKey}", _privateBucket, objectKey);
            return objectKey;
        }

        /// <inheritdoc/>
        public async Task<string> GetPresignedUrlAsync(string objectKey, int expirySeconds = 3600)
        {
            objectKey = NormalizeObjectKey(objectKey, _privateBucket);
            if (string.IsNullOrWhiteSpace(objectKey))
                throw new ArgumentException("Object key cannot be empty.", nameof(objectKey));

            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _privateBucket,
                    Key = objectKey,
                    Expires = DateTime.UtcNow.AddSeconds(expirySeconds),
                    Verb = HttpVerb.GET
                };

                var url = await Task.FromResult(_s3.GetPreSignedURL(request));

                _logger.LogInformation("Presigned URL generated for {ObjectKey}, expires in {Expiry}s", objectKey, expirySeconds);
                return url;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Failed to generate presigned URL for {ObjectKey}", objectKey);
                throw new Exception($"Could not generate presigned URL: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<(Stream Content, string ContentType, long ContentLength)?> GetObjectStreamAsync(string bucket, string objectKey)
        {
            objectKey = NormalizeObjectKey(objectKey, bucket);
            if (string.IsNullOrWhiteSpace(objectKey))
                return null;

            try
            {
                var request = new GetObjectRequest { BucketName = bucket, Key = objectKey };
                var response = await _s3.GetObjectAsync(request);
                var contentType = response.Headers.ContentType;
                if (string.IsNullOrWhiteSpace(contentType))
                    contentType = GetContentType(Path.GetExtension(objectKey));
                return (response.ResponseStream, contentType, response.ContentLength);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("R2 GetObject not found: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
                return null;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "R2 GetObject failed: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
                throw new Exception($"Failed to read object: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task DeletePrivateImageAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey)) return;
            await RemoveObjectAsync(_privateBucket, objectKey);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  Internal helpers
        // ═══════════════════════════════════════════════════════════════════════════

        private async Task PutObjectAsync(string bucket, string objectKey, byte[] fileBytes)
        {
            var extension   = Path.GetExtension(objectKey).ToLowerInvariant();
            var contentType = GetContentType(extension);

            try
            {
                using var stream = new MemoryStream(fileBytes);

                var request = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = objectKey,
                    InputStream = stream,
                    ContentType = contentType,
                    AutoCloseStream = true
                };

                await _s3.PutObjectAsync(request);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "R2 PutObject failed: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
                throw new Exception($"Image upload failed: {ex.Message}", ex);
            }
        }

        private async Task RemoveObjectAsync(string bucket, string objectKey)
        {
            try
            {
                var request = new DeleteObjectRequest { BucketName = bucket, Key = objectKey };

                await _s3.DeleteObjectAsync(request);
                _logger.LogInformation("Deleted: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "R2 RemoveObject failed: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
            }
        }

        private async Task EnsureBucketExistsAsync(string bucketName)
        {
            try
            {
                bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3, bucketName);

                if (!exists)
                {
                    await _s3.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
                    _logger.LogInformation("Created R2 bucket: {BucketName}", bucketName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise bucket: {BucketName}", bucketName);
            }

            // Note: R2 does not support anonymous public-read bucket policies via the
            // S3 API. Public access for the public bucket is enabled in the Cloudflare
            // dashboard (Public bucket custom domain) and is served via R2:PublicBaseUrl.
        }

        // ─── Static utility ───────────────────────────────────────────────────────

        private static void ValidateImage(byte[] fileBytes, string fileName)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("File bytes cannot be empty.");

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new ArgumentException($"Invalid file extension '{extension}'. Allowed: {string.Join(", ", AllowedExtensions)}");

            if (!HasValidImageMagicBytes(fileBytes))
                throw new ArgumentException("File content does not match a recognised image format (JPEG, PNG, WEBP, GIF).");
        }

        private static bool HasValidImageMagicBytes(byte[] fileBytes)
        {
            foreach (var (magic, offset) in AllowedMagicBytes)
            {
                if (fileBytes.Length < offset + magic.Length) continue;
                bool match = true;
                for (int i = 0; i < magic.Length; i++)
                    if (fileBytes[offset + i] != magic[i]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }

        private static string ExtractObjectKey(string url, string publicBaseUrl, string bucketName)
        {
            var prefix = $"{publicBaseUrl}/";
            if (!string.IsNullOrEmpty(publicBaseUrl) && url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return url.Substring(prefix.Length);

            return NormalizeObjectKey(url, bucketName);
        }

        private static string NormalizeObjectKey(string input, string bucketName)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var key = input.Trim();
            if (Uri.TryCreate(key, UriKind.Absolute, out var absolute) && !string.IsNullOrEmpty(absolute.AbsolutePath))
                key = absolute.AbsolutePath.TrimStart('/');

            // Legacy public URL format: https://host/files/{bucket}/{object-key}
            if (key.StartsWith("files/", StringComparison.OrdinalIgnoreCase))
                key = key.Substring("files/".Length);

            if (key.StartsWith(bucketName + "/", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(bucketName.Length + 1);

            var queryIndex = key.IndexOf('?');
            if (queryIndex >= 0)
                key = key.Substring(0, queryIndex);

            return Uri.UnescapeDataString(key);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string SanitizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return "images";
            var segments = folder
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)))
                .Where(s => !string.IsNullOrWhiteSpace(s));
            return segments.Any() ? string.Join("/", segments) : "images";
        }

        private static string GetContentType(string extension) => extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            _                 => "application/octet-stream"
        };
    }
}