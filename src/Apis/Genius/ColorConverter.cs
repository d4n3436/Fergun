using System;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Genius;

/// <summary>
/// Converts a string to a <see cref="Color"/>.
/// </summary>
public class ColorConverter : JsonConverter<Color>
{
    /// <inheritdoc/>
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Utf8Parser.TryParse(reader.ValueSpan.TrimStart((byte)'#'), out int color, out _, 'X') ? Color.FromArgb(color) : default;

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Converter is only used for deserialization.")]
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}