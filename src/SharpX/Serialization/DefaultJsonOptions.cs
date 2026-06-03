using System.Text.Json;

namespace SharpX.Serialization;

/// <summary>
/// Default <see cref="JsonSerializerOptions"/> used by SharpX when none are supplied.
/// </summary>
public static class DefaultJsonOptions
{
    /// <summary>The shared default options instance. Camel-case names, lenient reads, and ignored nulls.</summary>
    public static readonly JsonSerializerOptions Instance = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return options;
    }
}
