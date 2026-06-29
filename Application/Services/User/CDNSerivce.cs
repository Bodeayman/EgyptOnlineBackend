using EgyptOnline.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace EgyptOnline.Services
{
    /// <summary>
    /// MinIO-backed CDN service with two-bucket strategy:
    ///   • PUBLIC  bucket  → profile photos        → anonymous read, permanent URLs
    ///   • PRIVATE bucket  → KYC docs / receipts   → no public access, presigned URLs only
    /// </summary>
    public class MinioStorageService : ICDNService
    {
        private readonly IMinioClient _minio;
        private readonly string _publicBucket;
        private readonly string _privateBucket;
        private readonly string _publicBaseUrl;
        private readonly ILogger<MinioStorageService> _logger;

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

        public MinioStorageService(IConfiguration config, ILogger<MinioStorageService> logger)
        {
            _logger = logger;

            var endpoint  = config["Minio:Endpoint"]  ?? "localhost:9000";
            var accessKey = config["Minio:AccessKey"] ?? throw new InvalidOperationException("Minio:AccessKey is not configured.");
            var secretKey = config["Minio:SecretKey"] ?? throw new InvalidOperationException("Minio:SecretKey is not configured.");
            var useSSL    = bool.Parse(config["Minio:UseSSL"] ?? "false");

            _publicBucket  = config["Minio:PublicBucketName"]  ?? "egypt-online-public";
            _privateBucket = config["Minio:PrivateBucketName"] ?? "egypt-online-private";
            _publicBaseUrl = (config["Minio:PublicBaseUrl"] ?? $"http://{endpoint}/{_publicBucket}").TrimEnd('/');

            _minio = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSSL)
                .Build();

            // Ensure both buckets exist at startup
            _ = EnsureBucketExistsAsync(_publicBucket,  isPublic: true);
            _ = EnsureBucketExistsAsync(_privateBucket, isPublic: false);
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
            try
            {
                var args = new PresignedGetObjectArgs()
                    .WithBucket(_privateBucket)
                    .WithObject(objectKey)
                    .WithExpiry(expirySeconds);

                var url = await _minio.PresignedGetObjectAsync(args);
                _logger.LogInformation("Presigned URL generated for {ObjectKey}, expires in {Expiry}s", objectKey, expirySeconds);
                return url;
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "Failed to generate presigned URL for {ObjectKey}", objectKey);
                throw new Exception($"Could not generate presigned URL: {ex.Message}", ex);
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

                var args = new PutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey)
                    .WithStreamData(stream)
                    .WithObjectSize(fileBytes.Length)
                    .WithContentType(contentType);

                await _minio.PutObjectAsync(args);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO PutObject failed: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
                throw new Exception($"Image upload failed: {ex.Message}", ex);
            }
        }

        private async Task RemoveObjectAsync(string bucket, string objectKey)
        {
            try
            {
                var args = new RemoveObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey);

                await _minio.RemoveObjectAsync(args);
                _logger.LogInformation("Deleted: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
            }
            catch (MinioException ex)
            {
                _logger.LogError(ex, "MinIO RemoveObject failed: bucket={Bucket}, key={ObjectKey}", bucket, objectKey);
            }
        }

        private async Task EnsureBucketExistsAsync(string bucketName, bool isPublic)
        {
            try
            {
                var existsArgs = new BucketExistsArgs().WithBucket(bucketName);
                bool exists = await _minio.BucketExistsAsync(existsArgs);

                if (!exists)
                {
                    await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
                    _logger.LogInformation("Created MinIO bucket: {BucketName}", bucketName);
                }

                // Set anonymous read policy on the public bucket only
                if (isPublic)
                {
                    var policy = $$"""
                    {
                        "Version": "2012-10-17",
                        "Statement": [{
                            "Effect": "Allow",
                            "Principal": {"AWS": ["*"]},
                            "Action":    ["s3:GetObject"],
                            "Resource":  ["arn:aws:s3:::{{bucketName}}/*"]
                        }]
                    }
                    """;

                    var setPolicyArgs = new SetPolicyArgs()
                        .WithBucket(bucketName)
                        .WithPolicy(policy);

                    await _minio.SetPolicyAsync(setPolicyArgs);
                    _logger.LogInformation("Applied public-read policy to bucket: {BucketName}", bucketName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise bucket: {BucketName}", bucketName);
            }
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
            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return url.Substring(prefix.Length);

            var path = new Uri(url).AbsolutePath.TrimStart('/');
            return path.StartsWith(bucketName + "/", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(bucketName.Length + 1)
                : path;
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
