using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SharpX.Serialization;

/// <summary>
/// Represents a file attachment used in multipart form-data uploads.
/// </summary>
public sealed class FormFile
{
    /// <summary>The form field name to associate with this file.</summary>
    public string FieldName { get; }

    /// <summary>The file name reported to the server.</summary>
    public string FileName { get; }

    /// <summary>The file content. Owned by the caller; not disposed by SharpX.</summary>
    public Stream Content { get; }

    /// <summary>The content type. Defaults to application/octet-stream.</summary>
    public string ContentType { get; }

    /// <summary>Creates a new <see cref="FormFile"/>.</summary>
    public FormFile(string fieldName, string fileName, Stream content, string? contentType = null)
    {
        FieldName = string.IsNullOrEmpty(fieldName) ? throw new ArgumentException("Field name required.", nameof(fieldName)) : fieldName;
        FileName = string.IsNullOrEmpty(fileName) ? throw new ArgumentException("File name required.", nameof(fileName)) : fileName;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType!;
    }
}

/// <summary>
/// Marker payload that instructs SharpX to serialize as application/x-www-form-urlencoded.
/// </summary>
public sealed class UrlEncodedFormData
{
    /// <summary>The form fields.</summary>
    public IDictionary<string, object?> Fields { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Adds a field to the form.</summary>
    public UrlEncodedFormData Add(string key, object? value)
    {
        Fields[key] = value;
        return this;
    }
}

/// <summary>
/// Marker payload that instructs SharpX to serialize as multipart/form-data, including streaming files.
/// </summary>
public sealed class MultipartFormData
{
    /// <summary>The text fields included in the form.</summary>
    public IDictionary<string, object?> Fields { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>The file uploads included in the form.</summary>
    public IList<FormFile> Files { get; } = new List<FormFile>();

    /// <summary>Adds a text field.</summary>
    public MultipartFormData AddField(string key, object? value)
    {
        Fields[key] = value;
        return this;
    }

    /// <summary>Adds a file field.</summary>
    public MultipartFormData AddFile(FormFile file)
    {
        Files.Add(file ?? throw new ArgumentNullException(nameof(file)));
        return this;
    }
}

/// <summary>
/// Builds <see cref="HttpContent"/> instances from SharpX request payloads.
/// </summary>
internal static class RequestContentFactory
{
    public static HttpContent? Build(object? data, JsonSerializerOptions options, IDictionary<string, string> headers)
    {
        switch (data)
        {
            case null:
                return null;
            case HttpContent httpContent:
                return httpContent;
            case string s:
                {
                    var content = new StringContent(s, Encoding.UTF8, GuessTextContentType(headers));
                    return content;
                }
            case byte[] bytes:
                {
                    var content = new ByteArrayContent(bytes);
                    var ct = GetHeader(headers, "Content-Type") ?? "application/octet-stream";
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(ct);
                    return content;
                }
            case Stream stream:
                {
                    var content = new StreamContent(stream);
                    var ct = GetHeader(headers, "Content-Type") ?? "application/octet-stream";
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(ct);
                    return content;
                }
            case UrlEncodedFormData form:
                return BuildUrlEncoded(form);
            case MultipartFormData multipart:
                return BuildMultipart(multipart);
            default:
                return BuildJson(data, options);
        }
    }

    private static string GuessTextContentType(IDictionary<string, string> headers)
    {
        return GetHeader(headers, "Content-Type") ?? "text/plain";
    }

    private static string? GetHeader(IDictionary<string, string> headers, string name)
    {
        foreach (var kv in headers)
        {
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return null;
    }

    private static HttpContent BuildJson(object data, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(data, data.GetType(), options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return content;
    }

    private static HttpContent BuildUrlEncoded(UrlEncodedFormData form)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var kv in form.Fields)
        {
            if (kv.Value is null)
            {
                continue;
            }

            if (kv.Value is System.Collections.IEnumerable enumerable && kv.Value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    pairs.Add(new KeyValuePair<string, string>(kv.Key, FormatScalar(item)));
                }
            }
            else
            {
                pairs.Add(new KeyValuePair<string, string>(kv.Key, FormatScalar(kv.Value)));
            }
        }

        return new FormUrlEncodedContent(pairs);
    }

    private static HttpContent BuildMultipart(MultipartFormData multipart)
    {
        var boundary = "----SharpXBoundary" + Guid.NewGuid().ToString("N");
        var content = new MultipartFormDataContent(boundary);

        foreach (var kv in multipart.Fields)
        {
            if (kv.Value is null)
            {
                continue;
            }

            content.Add(new StringContent(FormatScalar(kv.Value), Encoding.UTF8), kv.Key);
        }

        foreach (var file in multipart.Files)
        {
            var streamContent = new StreamContent(file.Content);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(streamContent, file.FieldName, file.FileName);
        }

        return content;
    }

    private static string FormatScalar(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
