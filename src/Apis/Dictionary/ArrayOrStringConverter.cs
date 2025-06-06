﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a converter that handles strings that should be lists.
/// </summary>
public class ArrayOrStringConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<IReadOnlyList<string>>(ref reader)!,
            JsonTokenType.String => reader.ValueTextEquals(ReadOnlySpan<byte>.Empty) ? [] : [reader.GetString()!],
            _ => throw new JsonException("Token type must be either array or string.")
        };

    /// <inheritdoc />
    [ExcludeFromCodeCoverage(Justification = "Converter is only used for deserialization.")]
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}