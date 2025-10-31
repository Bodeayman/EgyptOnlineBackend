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

        public LocalStorageService(
            IConfiguration config,
            ILogger<LocalStorageService> logger)
        {
            _storageRoot = config["LocalStorage:RootPath"] ?? "/app/images";
            _imageServerBaseUrl = config["ImageServer:BaseUrl"] ?? "http://nginx";
            _logger = logger;

            // Ensure root directory exists
            if (!Directory.Exists(_storageRoot))
            {
                Directory.CreateDirectory(_storageRoot);
                _logger.LogInformation("Created storage root directory: {StorageRoot}", _storageRoot);
            }
        }

        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "")
        {
            try
            {
                // Sanitize inputs
                fileName = SanitizeFileName(fileName);
                folder = SanitizeFolder(folder);

                // Create target folder
                var targetFolder = Path.Combine(_storageRoot, folder);
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                    _logger.LogInformation("Created folder: {Folder}", targetFolder);
                }

                // Full file path
                var filePath = Path.Combine(targetFolder, fileName);

                // Write file
                await File.WriteAllBytesAsync(filePath, fileBytes);

                // Construct URL that Nginx will serve
                var relativePath = string.IsNullOrEmpty(folder)
                    ? fileName
                    : $"{folder}/{fileName}";

                var imageUrl = $"{_imageServerBaseUrl}/images/{relativePath}";

                _logger.LogInformation(
                    "Image uploaded successfully. Path: {FilePath}, URL: {ImageUrl}",
                    filePath,
                    imageUrl
                );

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
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return Task.CompletedTask;
                }

                // Extract relative path from URL
                // URL format: http://nginx/images/profiles/user123_guid.jpg
                var uri = new Uri(imageUrl);
                var relativePath = uri.AbsolutePath
                    .Replace("/images/", "")
                    .TrimStart('/');

                var fullPath = Path.Combine(_storageRoot, relativePath);

                // Security check: ensure path is within storage root
                var normalizedPath = Path.GetFullPath(fullPath);
                var normalizedRoot = Path.GetFullPath(_storageRoot);

                if (!normalizedPath.StartsWith(normalizedRoot))
                {
                    _logger.LogWarning("Attempted path traversal attack: {Path}", fullPath);
                    throw new UnauthorizedAccessException("Invalid file path");
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
                // Don't throw - deletion failures shouldn't break the app
            }

            return Task.CompletedTask;
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove path separators and dangerous characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars));

            // Ensure it's just the filename, no path
            return Path.GetFileName(sanitized);
        }

        private string SanitizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return string.Empty;
            }

            // Remove dangerous characters and path traversal attempts
            return folder
                .Trim('/', '\\')
                .Replace("..", "")
                .Replace("\\", "")
                .Replace(":", "")
                .Trim();
        }
    }
}