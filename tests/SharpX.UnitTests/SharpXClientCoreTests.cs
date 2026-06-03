using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Xunit;

namespace SharpX.UnitTests;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await _responder(request, cancellationToken);
    }
}

public class SharpXClientCoreTests
{
    private static SharpXClient BuildClient(StubHandler handler, Action<SharpXClientOptions>? configure = null)
    {
        var opts = new SharpXClientOptions
        {
            HttpMessageHandler = handler,
            Timeout = TimeSpan.FromSeconds(5),
        };
        configure?.Invoke(opts);
        return new SharpXClient(opts);
    }

    private sealed class Echo
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public async Task GetAsync_DeserializesJsonResponse()
    {
        var handler = new StubHandler((req, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"name\":\"test\",\"value\":42}", Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(resp);
        });

        using var client = BuildClient(handler, o => o.BaseUrl = "https://api.local");

        var resp = await client.GetAsync<Echo>("/items/1");

        resp.IsSuccess.Should().BeTrue();
        resp.Status.Should().Be(200);
        resp.Data.Should().NotBeNull();
        resp.Data!.Name.Should().Be("test");
        resp.Data.Value.Should().Be(42);
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://api.local/items/1");
    }

    [Fact]
    public async Task PostAsync_SerializesBodyAsJson()
    {
        string? capturedBody = null;
        var handler = new StubHandler(async (req, _) =>
        {
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}"),
            };
        });

        using var client = BuildClient(handler);
        var payload = new Echo { Name = "hi", Value = 7 };
        var resp = await client.PostAsync<Echo>("https://api.local/items", payload);

        resp.Status.Should().Be(201);
        capturedBody.Should().Contain("\"name\":\"hi\"").And.Contain("\"value\":7");
        handler.Requests[0].Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task NonSuccessStatus_ThrowsSharpXException()
    {
        var handler = new StubHandler((req, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found"),
        }));

        using var client = BuildClient(handler);
        var act = () => client.GetAsync<string>("https://x.local/missing");
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.Status.Should().Be(404);
        ex.Which.Category.Should().Be(SharpXErrorCategory.HttpStatus);
    }

    [Fact]
    public async Task ValidateStatus_AllowsCustomSuccess()
    {
        var handler = new StubHandler((req, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("\"ok\""),
        }));

        using var client = BuildClient(handler);
        var resp = await client.GetAsync<string>("https://x.local/m", new SharpXRequestConfig { ValidateStatus = s => s == 404 });
        resp.IsSuccess.Should().BeTrue();
        resp.Data.Should().Be("ok");
    }

    [Fact]
    public async Task Timeout_ThrowsTimeoutException()
    {
        var handler = new StubHandler(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = BuildClient(handler, o => o.Timeout = TimeSpan.FromMilliseconds(100));
        var act = () => client.GetAsync<string>("https://x.local/slow");
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.IsTimeout.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_ThrowsCancelledException()
    {
        var handler = new StubHandler(async (req, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = BuildClient(handler);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var act = () => client.GetAsync<string>("https://x.local/slow", cancellationToken: cts.Token);
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task QueryParams_AreAppendedToUrl()
    {
        var handler = new StubHandler((req, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\"ok\""),
        }));

        using var client = BuildClient(handler);
        var cfg = new SharpXRequestConfig();
        cfg.Params["q"] = "hello world";
        cfg.Params["page"] = 2;

        await client.GetAsync<string>("https://api.local/search", cfg);

        var url = handler.Requests[0].RequestUri!.AbsoluteUri;
        url.Should().Contain("q=hello%20world").And.Contain("page=2");
    }

    [Fact]
    public async Task RequestInterceptor_CanMutateConfig()
    {
        var handler = new StubHandler((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"ok\"") }));

        using var client = BuildClient(handler);
        client.RequestInterceptors.Use((cfg, _) =>
        {
            cfg.Headers["Authorization"] = "Bearer test";
            return Task.FromResult(cfg);
        });

        await client.GetAsync<string>("https://api.local/x");
        handler.Requests[0].Headers.Authorization!.ToString().Should().Be("Bearer test");
    }

    [Fact]
    public async Task ResponseInterceptor_CanRewriteBody()
    {
        var handler = new StubHandler((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"original\"") }));

        using var client = BuildClient(handler);
        client.ResponseInterceptors.Use((env, _) =>
        {
            env.RawBody = "\"rewritten\"";
            return Task.FromResult(env);
        });

        var resp = await client.GetAsync<string>("https://api.local/x");
        resp.Data.Should().Be("rewritten");
    }

    [Fact]
    public async Task TransformRequest_RunsBeforeSend()
    {
        string? sent = null;
        var handler = new StubHandler(async (req, _) =>
        {
            sent = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        using var client = BuildClient(handler);
        var cfg = new SharpXRequestConfig();
        cfg.TransformRequest.Add((data, headers) => new { wrapped = data });

        await client.PostAsync<object>("https://api.local/x", new { v = 1 }, cfg);
        sent.Should().Contain("wrapped");
    }

    [Fact]
    public async Task TransformResponse_RunsBeforeDeserialize()
    {
        var handler = new StubHandler((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"ignored\"") }));

        using var client = BuildClient(handler);
        var cfg = new SharpXRequestConfig();
        cfg.TransformResponse.Add((body, headers) => "\"transformed\"");

        var resp = await client.GetAsync<string>("https://api.local/x", cfg);
        resp.Data.Should().Be("transformed");
    }

    [Fact]
    public async Task FormUrlEncoded_SerializesCorrectly()
    {
        string? body = null;
        string? contentType = null;
        var handler = new StubHandler(async (req, _) =>
        {
            body = await req.Content!.ReadAsStringAsync();
            contentType = req.Content.Headers.ContentType!.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        using var client = BuildClient(handler);
        var form = new SharpX.Serialization.UrlEncodedFormData()
            .Add("user", "alice")
            .Add("ids", new[] { 1, 2 });

        await client.PostAsync<object>("https://api.local/login", form);
        contentType.Should().Be("application/x-www-form-urlencoded");
        body.Should().Contain("user=alice").And.Contain("ids=1").And.Contain("ids=2");
    }

    [Fact]
    public async Task DefaultHeaders_AreSentOnEveryRequest()
    {
        var handler = new StubHandler((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

        using var client = BuildClient(handler, o =>
        {
            o.DefaultHeaders["X-Client"] = "sharpx";
        });

        await client.GetAsync<object>("https://api.local/a");
        await client.GetAsync<object>("https://api.local/b");

        handler.Requests.Should().AllSatisfy(r => r.Headers.GetValues("X-Client").Should().ContainSingle().Which.Should().Be("sharpx"));
    }

    [Fact]
    public void HeaderInjection_IsRejected()
    {
        var handler = new StubHandler((req, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));
        using var client = BuildClient(handler);
        var cfg = new SharpXRequestConfig();
        cfg.Headers["X-Custom"] = "valid\r\nX-Injected: bad";

        var act = () => client.GetAsync<object>("https://api.local/x", cfg);
        act.Should().ThrowAsync<SharpXException>();
    }
}
