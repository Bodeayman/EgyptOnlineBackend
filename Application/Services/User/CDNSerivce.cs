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
        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "images")
        {
            try
            {
                if (fileBytes == null || fileBytes.Length == 0)
                    throw new ArgumentException("File bytes cannot be empty");

                // Sanitize inputs
                fileName = SanitizeFileName(fileName);
                folder = SanitizeFolder(folder);

                // Ensure target folder exists
                var targetFolder = Path.Combine(_storageRoot, folder);
                Directory.CreateDirectory(targetFolder);

                // Full file path
                var filePath = Path.Combine(targetFolder, fileName);

                // Write file
                await File.WriteAllBytesAsync(filePath, fileBytes);

                // FIX: Remove wwwroot from the path when constructing URL
                var relativePath = Path.Combine(folder, fileName).Replace("\\", "/");

                // Remove 'wwwroot/' or 'wwwroot\' from the beginning if present
                if (relativePath.StartsWith("wwwroot/") || relativePath.StartsWith("wwwroot\\"))
                {
                    relativePath = relativePath.Substring(8); // Remove "wwwroot/"
                }

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
