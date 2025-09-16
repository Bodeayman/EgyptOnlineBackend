using EgyptOnline.Domain.Interfaces;
using Imagekit.Sdk;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Services
{
    public class ImageKitService : ICDNService
    {
        private readonly ImagekitClient _imageKitClient;
        private readonly IConfiguration _config;

        public ImageKitService(IConfiguration config)
        {
            _config = config;


            _imageKitClient = new ImagekitClient(
                publicKey: config["CDN:PublicKey"],
                privateKey: config["CDN:PrivateKey"],
                urlEndPoint: config["CDN:URLEndPoint"]
            );
        }
        public async Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "/")
        {
            try
            {
                var uploadRequest = new FileCreateRequest
                {
                    file = fileBytes,
                    fileName = fileName,
                    folder = folder
                };

                var result = await _imageKitClient.UploadAsync(uploadRequest);


                if (result == null || string.IsNullOrEmpty(result.url))
                    throw new Exception("ImageKit upload failed or returned null URL");

                return result.url; // CDN URL
            }
            catch (Exception ex)
            {
                // Log error if needed
                throw new Exception($"Image upload failed: {ex.Message}", ex);
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