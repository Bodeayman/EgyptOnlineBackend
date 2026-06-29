using EgyptOnline.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EgyptOnline.Services
{
    public class LocalStorageService : ICDNService
    {
        private readonly string _storageRoot;
        private readonly string _imageServerBaseUrl;
        private readonly ILogger<LocalStorageService> _logger;

        public LocalStorageService(IConfiguration config, ILogger<LocalStorageService> logger)
        {
            _storageRoot = config["LocalStorage:RootPath"] ??
                           Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

            _imageServerBaseUrl = config["ImageServer:BaseUrl"] ??
                                 "http://localhost:5095/images";

            _logger = logger;

            try
            {
                Directory.CreateDirectory(_storageRoot);
                _logger.LogInformation("Storage root directory: {StorageRoot}", _storageRoot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create storage root directory: {StorageRoot}", _storageRoot);
                throw;
            }
        }
        /// <summary>
        /// Allowed image MIME magic-byte signatures.
        /// Each entry is (magic bytes, offset from start of file).
        /// </summary>
        private static readonly IReadOnlyList<(byte[] Magic, int Offset)> AllowedMagicBytes =
            new List<(byte[], int)>
            {
                // JPEG: FF D8 FF
                (new byte[] { 0xFF, 0xD8, 0xFF }, 0),
                // PNG: 89 50 4E 47 0D 0A 1A 0A
                (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),
                // GIF87a / GIF89a
                (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, 0),
                (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, 0),
                // WEBP: RIFF????WEBP  (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
                (new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0),
            };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        /// <summary>
        /// Validates that the file bytes start with a recognised image magic signature.
        /// </summary>
        private static bool HasValidImageMagicBytes(byte[] fileBytes)
        {
            foreach (var (magic, offset) in AllowedMagicBytes)
            {
                if (fileBytes.Length < offset + magic.Length)
                    continue;

                bool match = true;
                for (int i = 0; i < magic.Length; i++)
                {
                    if (fileBytes[offset + i] != magic[i]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }

        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "images")
        {
            try
            {
                if (fileBytes == null || fileBytes.Length == 0)
                    throw new ArgumentException("File bytes cannot be empty");

                // ── 1. Validate file extension ──────────────────────────────────
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    throw new ArgumentException($"Invalid file extension '{extension}'. Allowed: {string.Join(", ", AllowedExtensions)}");

                // ── 2. Validate magic bytes (prevents content-type spoofing) ────
                if (!HasValidImageMagicBytes(fileBytes))
                    throw new ArgumentException("File content does not match a recognised image format (JPEG, PNG, WEBP, GIF).");

                // ── 3. Sanitize inputs ──────────────────────────────────────────
                fileName = SanitizeFileName(fileName);
                folder = SanitizeFolder(folder);

                // ── 4. Ensure target folder exists ──────────────────────────────
                var targetFolder = Path.Combine(_storageRoot, folder);
                Directory.CreateDirectory(targetFolder);

                // ── 5. Resolve full path and prevent path-traversal ─────────────
                var filePath = Path.GetFullPath(Path.Combine(targetFolder, fileName));
                var normalizedRoot = Path.GetFullPath(_storageRoot);
                if (!filePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Path traversal attempt blocked: {FilePath}", filePath);
                    throw new ArgumentException("Invalid file path detected.");
                }

                // ── 6. Write file ───────────────────────────────────────────────
                await File.WriteAllBytesAsync(filePath, fileBytes);

                // ── 7. Build public URL (strip leading wwwroot/ if present) ─────
                var relativePath = Path.Combine(folder, fileName).Replace("\\", "/");
                if (relativePath.StartsWith("wwwroot/") || relativePath.StartsWith("wwwroot\\"))
                    relativePath = relativePath.Substring(8);

                var imageUrl = $"{_imageServerBaseUrl}/{relativePath}";
                _logger.LogInformation("Image uploaded successfully: {FilePath} -> {ImageUrl}", filePath, imageUrl);

                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image: {FileName}", fileName);
                throw new Exception($"Image upload failed: {ex.Message}", ex);
            }
        }
        public Task DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl)) return Task.CompletedTask;

                var uri = new Uri(imageUrl);
                var relativePath = uri.AbsolutePath.Replace("/images/", "").TrimStart('/');
                var fullPath = Path.Combine(_storageRoot, relativePath);

                // Security check
                var normalizedPath = Path.GetFullPath(fullPath);
                var normalizedRoot = Path.GetFullPath(_storageRoot);
                if (!normalizedPath.StartsWith(normalizedRoot))
                {
                    _logger.LogWarning("Path traversal attempt: {Path}", fullPath);
                    return Task.CompletedTask;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted image: {FilePath}", fullPath);
                }
                else
                {
                    _logger.LogWarning("Image not found for deletion: {FilePath}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image: {ImageUrl}", imageUrl);
            }

            return Task.CompletedTask;
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        private string SanitizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return "images";

            // Split by slashes and sanitize each folder segment
            var segments = folder.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)))
                                 .Where(s => !string.IsNullOrWhiteSpace(s));

            return segments.Any() ? Path.Combine(segments.ToArray()) : "images";
        }
    }
}
