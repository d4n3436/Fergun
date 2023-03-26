using System;
using System.Buffers.Text;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Bing;

/// <summary>
/// Converts a string to a <see cref="Color"/>.
/// </summary>
public class ColorConverter : JsonConverter<Color>
{
    /// <inheritdoc/>
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Utf8Parser.TryParse(reader.ValueSpan, out int color, out _, 'X') ? Color.FromArgb(color) : default;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToArgb().ToString("X"));
}