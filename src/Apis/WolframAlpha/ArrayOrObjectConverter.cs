using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a converter that handles objects that should be lists.
/// </summary>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
public class ArrayOrObjectConverter<T> : JsonConverter<IReadOnlyList<T>>
{
    /// <inheritdoc />
    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<IReadOnlyList<T>>(ref reader)!,
            JsonTokenType.StartObject => new[] { JsonSerializer.Deserialize<T>(ref reader)! },
            _ => throw new JsonException("Token type must be either array or object.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}