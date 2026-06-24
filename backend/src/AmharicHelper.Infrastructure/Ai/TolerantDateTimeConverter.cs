using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Parses a nullable DateTime from whatever the LLM produces. Accepts ISO dates,
/// common day/month/year formats, empty strings, and "null" — returning null rather
/// than throwing, so a single odd date never breaks the whole analysis.
/// </summary>
public class TolerantDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy", "d/M/yyyy",
        "dd.MM.yyyy", "MM/dd/yyyy", "dd-MM-yyyy"
    ];

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s) || s.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParseExact(s, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;

            return null; // unrecognized format — keep the description, drop the date
        }

        // Unexpected token type: skip it gracefully.
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
    }
}
