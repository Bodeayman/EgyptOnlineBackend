using EgyptOnline.Data;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Models;

namespace EgyptOnline.Services
{
    public class UserImageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICDNService _cdnService;

        public UserImageService(ApplicationDbContext context, ICDNService cdnService)
        {
            _context = context;
            _cdnService = cdnService;
        }

        public async Task<string?> UploadUserImageAsync(User user, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("No file uploaded");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    throw new ArgumentException("Invalid file type");

                const int maxFileSize = 5 * 1024 * 1024;
                if (file.Length > maxFileSize)
                    throw new ArgumentException("File too large");

                // Read file bytes
                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                // Delete old image if exists
                if (!string.IsNullOrEmpty(user.ImageUrl))
                {
                    try { await _cdnService.DeleteImageAsync(user.ImageUrl); }
                    catch { /* log but ignore */ }
                }

                // Upload new image
                var uniqueFileName = $"user_{user.Id}_{Guid.NewGuid()}{extension}";
                var imageUrl = await _cdnService.UploadImageAsync(fileBytes, uniqueFileName, "profiles");

                // Update user entity
                user.ImageUrl = imageUrl;
                await _context.SaveChangesAsync();

                return imageUrl;
            }
            catch (Exception ex)
            {
                // Log exception
                return null;
            }
        }
    }

}