using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Deserializes a List&lt;T&gt; from whatever the LLM produces. Despite a tool schema declaring
/// a field as an array, the model occasionally double-encodes it as a JSON string containing
/// JSON text instead — e.g. "requiredActions": "[{...}]" rather than a real array. Reparses that
/// string rather than throwing, so one oddly-shaped field never breaks the whole analysis.
/// </summary>
public class TolerantListConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(List<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(TolerantListConverter<>).MakeGenericType(itemType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal class TolerantListConverter<T> : JsonConverter<List<T>>
{
    // Without this, System.Text.Json short-circuits a JSON `null` before Read is ever called
    // (the default for reference types) and just assigns C# null — bypassing the null -> []
    // normalization below entirely.
    public override bool HandleNull => true;

    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return [];

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.String)
        {
            var inner = root.GetString();
            if (string.IsNullOrWhiteSpace(inner)) return [];
            using var innerDoc = JsonDocument.Parse(inner);
            return ReadArray(innerDoc.RootElement, options);
        }

        return ReadArray(root, options);
    }

    private static List<T> ReadArray(JsonElement arrayElement, JsonSerializerOptions options)
    {
        var list = new List<T>();
        if (arrayElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in arrayElement.EnumerateArray())
        {
            var value = item.Deserialize<T>(options);
            if (value is not null) list.Add(value);
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
