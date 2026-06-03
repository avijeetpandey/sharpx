using FluentAssertions;
using SharpX.Security;
using Xunit;

namespace SharpX.UnitTests.Security;

public class HeaderSanitizerTests
{
    [Fact]
    public void ValidateName_RejectsCrlf()
    {
        var ex = Assert.Throws<ArgumentException>(() => HeaderSanitizer.ValidateName("X-Bad\r\nInjection"));
        ex.ParamName.Should().Be("name");
    }

    [Fact]
    public void ValidateName_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => HeaderSanitizer.ValidateName(""));
    }

    [Fact]
    public void ValidateName_AcceptsValid()
    {
        HeaderSanitizer.ValidateName("X-Custom-Header").Should().Be("X-Custom-Header");
    }

    [Theory]
    [InlineData("value\r\nX-Injected: bad")]
    [InlineData("value\nbad")]
    [InlineData("value\rbad")]
    [InlineData("value\0bad")]
    public void SanitizeValue_RejectsForbiddenChars(string input)
    {
        Assert.Throws<ArgumentException>(() => HeaderSanitizer.SanitizeValue(input));
    }

    [Fact]
    public void SanitizeValue_AllowsNullAsEmpty()
    {
        HeaderSanitizer.SanitizeValue(null).Should().Be(string.Empty);
    }

    [Fact]
    public void SanitizeValue_AllowsNormalValue()
    {
        HeaderSanitizer.SanitizeValue("Bearer abc.def").Should().Be("Bearer abc.def");
    }
}

public class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactHeaders_MasksSensitiveKeys()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret",
            ["X-Api-Key"] = "key",
            ["Accept"] = "application/json",
        };

        var redacted = SensitiveDataRedactor.RedactHeaders(headers);

        redacted["Authorization"].Should().Be(SensitiveDataRedactor.Mask);
        redacted["X-Api-Key"].Should().Be(SensitiveDataRedactor.Mask);
        redacted["Accept"].Should().Be("application/json");
    }

    [Fact]
    public void RedactUrl_StripsUserInfo()
    {
        var url = "https://user:pass@example.com/path";
        var redacted = SensitiveDataRedactor.RedactUrl(url);
        redacted.Should().Be($"https://{SensitiveDataRedactor.Mask}@example.com/path");
    }

    [Fact]
    public void RedactUrl_LeavesCleanUrlAlone()
    {
        var url = "https://example.com/path";
        SensitiveDataRedactor.RedactUrl(url).Should().Be(url);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("api_key", true)]
    [InlineData("AccessToken", true)]
    [InlineData("name", false)]
    public void IsSensitiveKey_ClassifiesCorrectly(string key, bool expected)
    {
        SensitiveDataRedactor.IsSensitiveKey(key).Should().Be(expected);
    }
}
