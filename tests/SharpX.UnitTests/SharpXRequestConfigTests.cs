using FluentAssertions;
using Xunit;

namespace SharpX.UnitTests;

public class SharpXRequestConfigTests
{
    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var a = new SharpXRequestConfig { Url = "/x", Method = SharpXHttpMethod.Post };
        a.Headers["A"] = "1";
        a.Params["p"] = 2;

        var b = a.Clone();
        b.Headers["A"] = "changed";
        b.Params["p"] = 99;

        a.Headers["A"].Should().Be("1");
        a.Params["p"].Should().Be(2);
        b.Url.Should().Be("/x");
        b.Method.Should().Be(SharpXHttpMethod.Post);
    }

    [Fact]
    public void MergeWith_PrefersOtherValues()
    {
        var defaults = new SharpXRequestConfig { BaseUrl = "https://api", Url = "/old" };
        defaults.Headers["X-Default"] = "yes";

        var overrides = new SharpXRequestConfig { Url = "/new" };
        overrides.Headers["X-Override"] = "1";

        var merged = defaults.MergeWith(overrides);

        merged.BaseUrl.Should().Be("https://api");
        merged.Url.Should().Be("/new");
        merged.Headers.Should().ContainKey("X-Default").And.ContainKey("X-Override");
    }
}
