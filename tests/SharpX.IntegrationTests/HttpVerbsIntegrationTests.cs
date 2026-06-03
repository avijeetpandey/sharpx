using System.Net;
using FluentAssertions;
using SharpX.Serialization;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace SharpX.IntegrationTests;

public sealed class WireMockFixture : IDisposable
{
    public WireMockServer Server { get; }

    public WireMockFixture()
    {
        Server = WireMockServer.Start();
    }

    public string Url => Server.Url!;

    public void Dispose() => Server.Dispose();
}

public class HttpVerbsIntegrationTests : IClassFixture<WireMockFixture>
{
    private readonly WireMockFixture _fx;

    public HttpVerbsIntegrationTests(WireMockFixture fx) => _fx = fx;

    private SharpXClient NewClient() => SharpXClient.Create(o =>
    {
        o.BaseUrl = _fx.Url;
        o.Timeout = TimeSpan.FromSeconds(10);
    });

    [Fact]
    public async Task Get_ReturnsExpectedJson()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/users/1").UsingGet())
                  .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = 1, name = "ada" }));

        using var client = NewClient();
        var resp = await client.GetAsync<Dictionary<string, object>>("/users/1");
        resp.Status.Should().Be(200);
        resp.Data!["name"].ToString().Should().Be("ada");
    }

    [Fact]
    public async Task Post_SendsJsonBody()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/users").UsingPost())
                  .RespondWith(Response.Create().WithStatusCode(201).WithBody("{\"ok\":true}"));

        using var client = NewClient();
        var resp = await client.PostAsync<Dictionary<string, object>>("/users", new { name = "neo" });
        resp.Status.Should().Be(201);
    }

    [Fact]
    public async Task Put_Patch_Delete()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/r/1").UsingPut()).RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));
        _fx.Server.Given(Request.Create().WithPath("/r/1").UsingPatch()).RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));
        _fx.Server.Given(Request.Create().WithPath("/r/1").UsingDelete()).RespondWith(Response.Create().WithStatusCode(204));

        using var client = NewClient();
        (await client.PutAsync<object>("/r/1", new { v = 1 })).Status.Should().Be(200);
        (await client.PatchAsync<object>("/r/1", new { v = 2 })).Status.Should().Be(200);
        (await client.DeleteAsync<object>("/r/1")).Status.Should().Be(204);
    }

    [Fact]
    public async Task Timeout_TriggersTimeoutCategory()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/slow").UsingGet())
                  .RespondWith(Response.Create().WithDelay(TimeSpan.FromSeconds(2)).WithBody("{}"));

        using var client = SharpXClient.Create(o =>
        {
            o.BaseUrl = _fx.Url;
            o.Timeout = TimeSpan.FromMilliseconds(200);
        });

        var act = () => client.GetAsync<object>("/slow");
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.IsTimeout.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_TriggersCancelledCategory()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/slow2").UsingGet())
                  .RespondWith(Response.Create().WithDelay(TimeSpan.FromSeconds(3)).WithBody("{}"));

        using var client = NewClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var act = () => client.GetAsync<object>("/slow2", cancellationToken: cts.Token);
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task FormUrlEncoded_PostsFields()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/login").UsingPost()
                .WithBody(b => b!.Contains("user=alice")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"ok\":true}"));

        using var client = NewClient();
        var form = new UrlEncodedFormData().Add("user", "alice").Add("pwd", "secret");
        var resp = await client.PostAsync<Dictionary<string, object>>("/login", form);
        resp.Status.Should().Be(200);
    }

    [Fact]
    public async Task MultipartFormData_UploadsFile()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/upload").UsingPost())
                  .RespondWith(Response.Create().WithStatusCode(201).WithBody("{\"ok\":true}"));

        using var client = NewClient();
        var bytes = System.Text.Encoding.UTF8.GetBytes("hello-file");
        using var ms = new MemoryStream(bytes);
        var multipart = new MultipartFormData()
            .AddField("title", "greeting")
            .AddFile(new FormFile("file", "hello.txt", ms, "text/plain"));

        var resp = await client.PostAsync<Dictionary<string, object>>("/upload", multipart);
        resp.Status.Should().Be(201);

        var requests = _fx.Server.LogEntries.Where(e => e.RequestMessage.Path == "/upload").ToList();
        requests.Should().HaveCount(1);
        requests[0].RequestMessage.Headers!["Content-Type"][0].Should().Contain("multipart/form-data");
    }

    [Fact]
    public async Task Interceptor_AddsBearerToken()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/secure").UsingGet().WithHeader("Authorization", "Bearer abc.def"))
                  .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"ok\":true}"));
        _fx.Server.Given(Request.Create().WithPath("/secure").UsingGet())
                  .RespondWith(Response.Create().WithStatusCode(401).WithBody("denied"));

        using var client = NewClient();
        client.RequestInterceptors.Use((cfg, _) =>
        {
            cfg.Headers["Authorization"] = "Bearer abc.def";
            return Task.FromResult(cfg);
        });

        var resp = await client.GetAsync<Dictionary<string, object>>("/secure");
        resp.Status.Should().Be(200);
    }

    [Fact]
    public async Task NotFound_ThrowsHttpStatusCategory()
    {
        _fx.Server.Reset();
        _fx.Server.Given(Request.Create().WithPath("/nope").UsingGet())
                  .RespondWith(Response.Create().WithStatusCode(404).WithBody("missing"));

        using var client = NewClient();
        var act = () => client.GetAsync<object>("/nope");
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.Status.Should().Be(404);
        ex.Which.Category.Should().Be(SharpXErrorCategory.HttpStatus);
        ex.Which.ResponseBody.Should().Contain("missing");
    }
}
