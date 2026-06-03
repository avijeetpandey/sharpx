# SharpX

[![CI](https://github.com/avijeetpandey/sharpx/actions/workflows/ci.yml/badge.svg)](https://github.com/avijeetpandey/sharpx/actions/workflows/ci.yml)

**SharpX** is a production-ready, axios-style HTTP client for .NET. It wraps `HttpClient`
with a small, expressive surface that mirrors the JavaScript [axios](https://github.com/axios/axios)
library while staying idiomatic to C# (`async`/`await`, `CancellationToken`, `System.Text.Json`).

## Features

- Strongly-typed `GetAsync<T>`, `PostAsync<T>`, `PutAsync<T>`, `PatchAsync<T>`, `DeleteAsync<T>`
- Request and response **interceptors** with `use` / `eject` semantics
- Per-request and per-instance **timeouts** + native **CancellationToken** support
- Automatic **JSON** serialization / deserialization via `System.Text.Json`
- Automatic **form-urlencoded** and streaming **multipart/form-data** payloads
- Robust **query string** serialization (objects, dictionaries, arrays)
- `TransformRequest` / `TransformResponse` hooks for raw payload mutation
- Configurable **status validator** (`ValidateStatus`)
- **Security hardening**: header injection prevention, sensitive header redaction in exceptions
- Isolated client instances via `SharpXClient.Create(...)`
- Targets `netstandard2.1` and `net8.0`

## Installation

```bash
dotnet add package SharpX
```

## Getting started

```csharp
using SharpX;

using var client = SharpXClient.Create(o =>
{
    o.BaseUrl = "https://api.example.com";
    o.Timeout = TimeSpan.FromSeconds(10);
});

var resp = await client.GetAsync<UserDto>("/users/1");
Console.WriteLine($"{resp.Status}: {resp.Data!.Name}");
```

### POST with JSON body

```csharp
var created = await client.PostAsync<UserDto>("/users", new { name = "Ada", email = "ada@x.io" });
```

## Interceptors

```csharp
var handle = client.RequestInterceptors.Use((cfg, ct) =>
{
    cfg.Headers["Authorization"] = $"Bearer {await tokenProvider.GetAsync(ct)}";
    return Task.FromResult(cfg);
});

client.ResponseInterceptors.Use((env, ct) =>
{
    if ((int)env.Message.StatusCode == 401)
    {
        // refresh logic
    }
    return Task.FromResult(env);
});

// Remove later
client.RequestInterceptors.Eject(handle);
```

## CancellationToken

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
try
{
    var resp = await client.GetAsync<Foo>("/slow", cancellationToken: cts.Token);
}
catch (SharpXException ex) when (ex.IsCancelled || ex.IsTimeout)
{
    // graceful handling
}
```

## Form data

```csharp
using SharpX.Serialization;

// application/x-www-form-urlencoded
var form = new UrlEncodedFormData().Add("user", "ada").Add("remember", true);
await client.PostAsync<object>("/login", form);

// multipart/form-data with streaming files
using var fs = File.OpenRead("avatar.png");
var multipart = new MultipartFormData()
    .AddField("title", "avatar")
    .AddFile(new FormFile("file", "avatar.png", fs, "image/png"));

await client.PostAsync<object>("/upload", multipart);
```

## Custom instances

```csharp
var billing = SharpXClient.Create(o =>
{
    o.BaseUrl = "https://billing.internal";
    o.DefaultHeaders["X-Tenant"] = "acme";
});

var analytics = SharpXClient.Create(o =>
{
    o.BaseUrl = "https://analytics.internal";
    o.Timeout = TimeSpan.FromSeconds(5);
});
```

## Error handling

Every failure is wrapped in a `SharpXException` with rich, **redacted** context:

```csharp
try
{
    var resp = await client.GetAsync<Foo>("/missing");
}
catch (SharpXException ex)
{
    // Authorization, Cookie, X-Api-Key, etc. are masked in ex.RequestConfig.Headers
    Console.WriteLine($"category={ex.Category} status={ex.Status} body={ex.ResponseBody}");
}
```

`SharpXErrorCategory` includes `HttpStatus`, `Timeout`, `Cancelled`, `Network`, `Deserialization`,
`Interceptor`, and `InvalidConfiguration` so callers can branch precisely.

### Custom `ValidateStatus`

```csharp
var resp = await client.GetAsync<Foo>("/maybe-404", new SharpXRequestConfig
{
    ValidateStatus = s => s == 404 || (s >= 200 && s < 300),
});
```

## Security

- Header values containing CR / LF / NUL are rejected before they hit the wire.
- Sensitive header keys (`Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`,
  `X-Auth-Token`, `Proxy-Authorization`, etc.) are masked in `SharpXException`.
- URL credentials (`https://user:pass@host`) are stripped from exception text.

## Project layout

```
src/SharpX                  → library (netstandard2.1 + net8.0)
tests/SharpX.UnitTests      → fast unit tests (xUnit, Moq, FluentAssertions)
tests/SharpX.IntegrationTests → WireMock.Net-backed HTTP tests
tests/SharpX.E2ETests       → full auth-flow scenarios
samples/SharpX.Examples     → runnable console examples
```

## Building and testing

```bash
dotnet build -c Release
dotnet test  -c Release
dotnet run --project samples/SharpX.Examples
```

## Contributing

Issues and pull requests are welcome. Please run `dotnet test` before submitting.
