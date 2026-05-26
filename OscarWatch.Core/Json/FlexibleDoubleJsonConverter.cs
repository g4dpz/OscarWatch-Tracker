using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OscarWatch.Core.Json;

/// <summary>Accepts JSON numbers or numeric strings (published database may mix both).</summary>
public sealed class FlexibleDoubleJsonConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => throw new JsonException($"Expected number or numeric string, got {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);

    private static double ParseString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        throw new JsonException($"Could not parse numeric string '{text}'.");
    }
}
