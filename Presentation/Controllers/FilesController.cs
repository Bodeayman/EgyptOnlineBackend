using EgyptOnline.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EgyptOnline.Controllers
{
    /// <summary>
    /// Backward-compatibility route for legacy file URLs stored in the database:
    ///   https://{host}/files/{bucket}/{object-key}
    /// Serves or redirects to the corresponding Cloudflare R2 object without
    /// requiring any DNS change. Also usable as the durable target for the
    /// reverse proxy's /files/ location when MinIO is retired.
    /// </summary>
    [ApiController]
    [DisableRateLimiting]
    public class FilesController : ControllerBase
    {
        private const string PublicBucket = "egypt-online-public";
        private const string PrivateBucket = "egypt-online-private";

        private readonly ICDNService _cdnService;

        public FilesController(ICDNService cdnService)
        {
            _cdnService = cdnService;
        }

        [HttpGet("files/{bucket}/{*key}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(string bucket, string key)
        {
            if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(key))
                return NotFound();

            var isPrivate = bucket.Equals(PrivateBucket, StringComparison.OrdinalIgnoreCase);
            if (!isPrivate && !bucket.Equals(PublicBucket, StringComparison.OrdinalIgnoreCase))
                return NotFound();

            if (isPrivate)
            {
                if (User.Identity?.IsAuthenticated != true)
                    return Unauthorized();

                // Legacy private references are served through the API (auth required).
                var result = await _cdnService.GetObjectStreamAsync(bucket, key);
                if (result == null)
                    return NotFound();

                return File(result.Value.Content, result.Value.ContentType, enableRangeProcessing: true);
            }

            // Public objects are served directly by R2/Cloudflare — redirect, never proxy the bytes.
            var publicBase = _cdnService.PublicBaseUrl;
            return Redirect($"{publicBase.TrimEnd('/')}/{key.TrimStart('/')}");
        }
    }
}