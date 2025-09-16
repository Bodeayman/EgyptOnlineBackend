using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Domain.Interfaces
{
    public interface ICDNService
    {
        public Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "/");

    }
}