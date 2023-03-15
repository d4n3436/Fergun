using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Fergun.Extensions;

public static class JsonExtensions
{
    public static IEnumerable<JsonElement> EnumerateArrayOrEmpty(this JsonElement element)
        => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : Enumerable.Empty<JsonElement>();

    public static JsonElement FirstOrDefault(this JsonElement element)
        => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().FirstOrDefault() : default;

    public static JsonElement FirstOrDefault(this JsonElement element, Func<JsonElement, bool> predicate)
        => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().FirstOrDefault(predicate) : default;

    public static JsonElement GetPropertyOrDefault(this JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) ? value : default;

    public static string? GetStringOrDefault(this JsonElement element)
        => element.ValueKind == JsonValueKind.String ? element.GetString() : default;
}