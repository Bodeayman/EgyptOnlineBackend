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
        /// Root of the R2 public bucket custom domain, e.g. https://cdn.example.com.
        /// Public file URLs are built as {PublicBaseUrl}/{object-key} — no bucket,
        /// /files, /profiles or /posts segments are appended to the base.
        /// </summary>
        string PublicBaseUrl { get; }

        /// <summary>
        /// Downloads an object's content stream from a bucket. The object key is
        /// normalized internally, so full URLs (legacy or R2) are accepted too.
        /// Returns null when the object does not exist. The caller owns the stream.
        /// </summary>
        Task<(Stream Content, string ContentType, long ContentLength)?> GetObjectStreamAsync(string bucket, string objectKey);

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