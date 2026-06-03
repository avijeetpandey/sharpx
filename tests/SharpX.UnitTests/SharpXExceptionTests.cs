using System.Net;
using FluentAssertions;
using Xunit;

namespace SharpX.UnitTests;

public class SharpXExceptionTests
{
    [Fact]
    public void Constructor_RedactsSensitiveHeaders()
    {
        var config = new SharpXRequestConfig { Url = "https://user:pwd@example.com/path" };
        config.Headers["Authorization"] = "Bearer top-secret";
        config.Headers["Accept"] = "application/json";

        var ex = new SharpXException(
            "boom",
            requestConfig: config,
            statusCode: HttpStatusCode.InternalServerError,
            category: SharpXErrorCategory.HttpStatus);

        ex.RequestConfig!.Headers["Authorization"].Should().NotContain("top-secret");
        ex.RequestConfig.Url.Should().NotContain("pwd");
        ex.Status.Should().Be(500);
    }
}
