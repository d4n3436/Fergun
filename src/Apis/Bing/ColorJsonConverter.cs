using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Bing;

/// <summary>
/// Converts a string to a <see cref="Color"/>.
/// </summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    /// <inheritdoc/>
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => int.TryParse(reader.GetString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color) ? Color.FromArgb(color) : default;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Color colorValue, JsonSerializerOptions options)
        => writer.WriteStringValue(colorValue.ToArgb().ToString("X"));
}