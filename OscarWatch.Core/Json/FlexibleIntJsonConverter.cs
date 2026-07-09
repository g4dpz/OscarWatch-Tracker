using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OscarWatch.Core.Json;

/// <summary>Accepts JSON numbers, numeric strings, or null (published catalogues may omit fields).</summary>
public sealed class FlexibleIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt32(out var i) ? i : (int)reader.GetDouble(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => throw new JsonException($"Expected number or numeric string, got {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);

    private static int ParseString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var asDouble))
            return (int)asDouble;

        throw new JsonException($"Could not parse integer string '{text}'.");
    }
}
