namespace EgyptOnline.Domain.Interfaces
{
    public interface ICDNService
    {
        /// <summary>
        /// Uploads an image to the PUBLIC bucket (e.g., profile photos).
        /// The returned URL is a permanent, directly accessible public URL.
        /// </summary>
        Task<string> UploadImageAsync(byte[] fileBytes, string fileName, string folder = "images");

        /// <summary>
        /// Uploads an image to the PRIVATE bucket (e.g., KYC documents, deposit receipts).
        /// The returned value is the internal object key (not a public URL).
        /// Use <see cref="GetPresignedUrlAsync"/> to generate a time-limited URL for viewing.
        /// </summary>
        Task<string> UploadPrivateImageAsync(byte[] fileBytes, string fileName, string folder);

        /// <summary>
        /// Generates a short-lived presigned URL for a private object.
        /// Default expiry is 1 hour.
        /// </summary>
        Task<string> GetPresignedUrlAsync(string objectKey, int expirySeconds = 3600);

        /// <summary>
        /// Deletes an image from the public bucket by its public URL.
        /// </summary>
        Task DeleteImageAsync(string imageUrl);

        /// <summary>
        /// Deletes an image from the private bucket by its object key.
        /// </summary>
        Task DeletePrivateImageAsync(string objectKey);
    }
}