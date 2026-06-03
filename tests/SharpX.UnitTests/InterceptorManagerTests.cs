using FluentAssertions;
using SharpX.Interceptors;
using Xunit;

namespace SharpX.UnitTests.Interceptors;

public class InterceptorManagerTests
{
    [Fact]
    public void Use_ReturnsHandle_AndSnapshotIsOrdered()
    {
        var manager = new InterceptorManager<RequestInterceptorDelegate>();
        var calls = new List<string>();
        var h1 = manager.Use((c, _) => { calls.Add("a"); return Task.FromResult(c); });
        var h2 = manager.Use((c, _) => { calls.Add("b"); return Task.FromResult(c); });

        h1.Should().NotBe(h2);
        manager.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public async Task Eject_RemovesInterceptor()
    {
        var manager = new InterceptorManager<RequestInterceptorDelegate>();
        var calls = new List<string>();
        var h1 = manager.Use((c, _) => { calls.Add("a"); return Task.FromResult(c); });
        manager.Use((c, _) => { calls.Add("b"); return Task.FromResult(c); });

        manager.Eject(h1).Should().BeTrue();
        manager.Eject(h1).Should().BeFalse();

        foreach (var (fn, _) in manager.Snapshot())
        {
            await fn(new SharpXRequestConfig(), default);
        }

        calls.Should().ContainSingle().Which.Should().Be("b");
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var manager = new InterceptorManager<RequestInterceptorDelegate>();
        manager.Use((c, _) => Task.FromResult(c));
        manager.Use((c, _) => Task.FromResult(c));
        manager.Clear();
        manager.Snapshot().Should().BeEmpty();
    }
}
