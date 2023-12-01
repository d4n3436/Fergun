using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a converter that decodes HTML-encoded strings.
/// </summary>
public class HtmlEncodingConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => WebUtility.HtmlDecode(reader.GetString());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}