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

                // Upload the new image FIRST so a failed upload leaves the
                // existing profile photo intact instead of deleting it.
                var oldImageUrl = user.ImageUrl;
                var uniqueFileName = $"user_{user.Id}_{Guid.NewGuid()}{extension}";
                var imageUrl = await _cdnService.UploadImageAsync(fileBytes, uniqueFileName, "profiles");

                // Update user entity, then delete the old image best-effort.
                user.ImageUrl = imageUrl;
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(oldImageUrl))
                {
                    try { await _cdnService.DeleteImageAsync(oldImageUrl); }
                    catch { /* log but ignore */ }
                }

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
