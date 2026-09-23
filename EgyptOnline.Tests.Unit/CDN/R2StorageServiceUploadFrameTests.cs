using System.IO;
using System.Text;
using Amazon.S3.Model;
using EgyptOnline.Services;
using Xunit;

namespace EgyptOnline.Tests.Unit.CDN;

/// <summary>
/// Pins the Cloudflare R2-compatible upload framing. AWSSDK.S3 streams uploads
/// with SigV4 chunked payload signing (STREAMING-AWS4-HMAC-SHA256-PAYLOAD) by
/// default, which R2 rejects. The request must disable payload signing so the
/// SDK sends x-amz-content-sha256: UNSIGNED-PAYLOAD.
/// </summary>
[Trait("Category", "Unit")]
public class R2StorageServiceUploadFrameTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public void BuildPutObjectRequest_DisablesPayloadSigning_ForR2(string contentType)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("image-bytes"));

        var request = R2StorageService.BuildPutObjectRequest("egypt-online-public", "images/1.jpg", contentType, stream);

        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.Equal("egypt-online-public", request.BucketName);
        Assert.Equal("images/1.jpg", request.Key);
        Assert.Equal(contentType, request.ContentType);
        Assert.Same(stream, request.InputStream);
    }

    [Fact]
    public void BuildPutObjectRequest_PreservesContentTypeFallback()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("image-bytes"));

        var request = R2StorageService.BuildPutObjectRequest("b", "k.jpg", "application/octet-stream", stream);

        Assert.Equal("application/octet-stream", request.ContentType);
    }
}