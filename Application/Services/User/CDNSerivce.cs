using EgyptOnline.Domain.Interfaces;
using Imagekit.Sdk;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Services
{
    public class LocalStorageService : ICDNService
    {
        private readonly string _storageRoot;

        public LocalStorageService(IConfiguration config)
        {
            // Path inside the container where the volume is mounted
            _storageRoot = config["LocalStorage:RootPath"] ?? "/app/images";

            // Ensure folder exists
            if (!Directory.Exists(_storageRoot))
            {
                Directory.CreateDirectory(_storageRoot);
            }
        }

        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "")
        {
            try
            {
                // Combine root + optional folder
                var targetFolder = Path.Combine(_storageRoot, folder.Trim('/'));
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // Full path for the image
                var filePath = Path.Combine(targetFolder, fileName);

                await File.WriteAllBytesAsync(filePath, fileBytes);

                // Return path relative to your API or public URL path
                // e.g., "/images/folder/fileName.jpg"
                var relativePath = Path.Combine("/images", folder.Trim('/'), fileName).Replace("\\", "/");
                return relativePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Local image upload failed: {ex.Message}", ex);
            }
        }
    }

    internal class UploadRequest : FileCreateRequest
    {
        public byte[] File { get; set; }
        public string FileName { get; set; }
        public string Folder { get; set; }
    }
}