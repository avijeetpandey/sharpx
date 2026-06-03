using System.Text;
using SharpX;
using SharpX.Serialization;

namespace SharpX.Examples;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("== SharpX examples ==");

        await BasicRequest();
        await BearerInterceptor();
        await CancelledRequest();
        await FileUpload();
    }

    private static async Task BasicRequest()
    {
        Console.WriteLine("\n[1] Basic GET against https://httpbin.org/get");
        try
        {
            using var client = SharpXClient.Create(o => o.Timeout = TimeSpan.FromSeconds(15));
            var resp = await client.GetAsync<Dictionary<string, object>>("https://httpbin.org/get");
            Console.WriteLine($"  status={resp.Status} keys=[{string.Join(", ", resp.Data!.Keys)}]");
        }
        catch (SharpXException ex)
        {
            Console.WriteLine($"  failed: category={ex.Category} message={ex.Message}");
        }
    }

    private static async Task BearerInterceptor()
    {
        Console.WriteLine("\n[2] Request interceptor injecting a Bearer token");
        try
        {
            using var client = SharpXClient.Create(o =>
            {
                o.BaseUrl = "https://httpbin.org";
                o.Timeout = TimeSpan.FromSeconds(15);
            });

            client.RequestInterceptors.Use((cfg, _) =>
            {
                cfg.Headers["Authorization"] = "Bearer demo.token.value";
                return Task.FromResult(cfg);
            });

            var resp = await client.GetAsync<Dictionary<string, object>>("/headers");
            Console.WriteLine($"  status={resp.Status}");
        }
        catch (SharpXException ex)
        {
            Console.WriteLine($"  failed: {ex.Message}");
        }
    }

    private static async Task CancelledRequest()
    {
        Console.WriteLine("\n[3] Cancellation example (cancels after 50ms)");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        using var client = SharpXClient.Create(o => o.Timeout = TimeSpan.FromSeconds(15));
        try
        {
            await client.GetAsync<object>("https://httpbin.org/delay/3", cancellationToken: cts.Token);
            Console.WriteLine("  unexpectedly succeeded");
        }
        catch (SharpXException ex)
        {
            Console.WriteLine($"  cancelled={ex.IsCancelled} timeout={ex.IsTimeout} category={ex.Category}");
        }
    }

    private static async Task FileUpload()
    {
        Console.WriteLine("\n[4] Multipart file upload");
        try
        {
            using var client = SharpXClient.Create(o => o.Timeout = TimeSpan.FromSeconds(15));
            var bytes = Encoding.UTF8.GetBytes("hello from sharpx");
            using var ms = new MemoryStream(bytes);

            var form = new MultipartFormData()
                .AddField("title", "demo")
                .AddFile(new FormFile("file", "demo.txt", ms, "text/plain"));

            var resp = await client.PostAsync<Dictionary<string, object>>("https://httpbin.org/post", form);
            Console.WriteLine($"  status={resp.Status}");
        }
        catch (SharpXException ex)
        {
            Console.WriteLine($"  failed: {ex.Message}");
        }
    }
}
