using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace SharpX.E2ETests;

public class AuthFlowEndToEndTests : IDisposable
{
    private readonly WireMockServer _server;

    public AuthFlowEndToEndTests()
    {
        _server = WireMockServer.Start();
        _server.Given(Request.Create().WithPath("/auth/login").UsingPost())
               .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { token = "abc.def.ghi" }));

        _server.Given(Request.Create().WithPath("/me").UsingGet().WithHeader("Authorization", "Bearer abc.def.ghi"))
               .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { id = 1, name = "ada" }));

        _server.Given(Request.Create().WithPath("/me").UsingGet())
               .RespondWith(Response.Create().WithStatusCode(401));

        _server.Given(Request.Create().WithPath("/auth/logout").UsingPost().WithHeader("Authorization", "Bearer abc.def.ghi"))
               .RespondWith(Response.Create().WithStatusCode(204));
    }

    public void Dispose() => _server.Dispose();

    private sealed class TokenResponse { public string Token { get; set; } = string.Empty; }
    private sealed class User { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

    [Fact]
    public async Task LoginFetchLogout_FullFlow()
    {
        using var client = SharpXClient.Create(o =>
        {
            o.BaseUrl = _server.Url;
            o.Timeout = TimeSpan.FromSeconds(10);
        });

        // Step 1: login
        var login = await client.PostAsync<TokenResponse>("/auth/login", new { user = "ada", password = "secret" });
        login.IsSuccess.Should().BeTrue();
        login.Data!.Token.Should().NotBeNullOrEmpty();

        // Step 2: register an interceptor that injects the bearer token
        var token = login.Data.Token;
        var handle = client.RequestInterceptors.Use((cfg, _) =>
        {
            cfg.Headers["Authorization"] = $"Bearer {token}";
            return Task.FromResult(cfg);
        });

        // Step 3: fetch protected resource
        var me = await client.GetAsync<User>("/me");
        me.Status.Should().Be(200);
        me.Data!.Name.Should().Be("ada");

        // Step 4: logout
        var logout = await client.PostAsync<object>("/auth/logout");
        logout.Status.Should().Be(204);

        // Step 5: eject interceptor and verify the protected endpoint now fails
        client.RequestInterceptors.Eject(handle).Should().BeTrue();

        var act = () => client.GetAsync<User>("/me");
        var ex = await act.Should().ThrowAsync<SharpXException>();
        ex.Which.Status.Should().Be(401);
    }

    [Fact]
    public async Task FailedLogin_RedactsCredentialsInException()
    {
        _server.Given(Request.Create().WithPath("/auth/fail").UsingPost())
               .RespondWith(Response.Create().WithStatusCode(403).WithBody("forbidden"));

        using var client = SharpXClient.Create(o => o.BaseUrl = _server.Url);
        var cfg = new SharpXRequestConfig();
        cfg.Headers["Authorization"] = "Bearer super-secret";

        var act = () => client.PostAsync<object>("/auth/fail", new { user = "x", password = "y" }, cfg);
        var ex = await act.Should().ThrowAsync<SharpXException>();

        ex.Which.RequestConfig!.Headers["Authorization"].Should().NotContain("super-secret");
        ex.Which.Status.Should().Be(403);
    }
}
