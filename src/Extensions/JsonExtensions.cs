using System.Text.Json;

namespace Fergun.Extensions
{
    public static class JsonExtensions
    {
        public static JsonElement GetPropertyOrDefault(this JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) ? value : default;

        public static string GetStringOrDefault(this JsonElement element)
            => element.ValueKind == JsonValueKind.String ? element.GetString() : default;

        public static int GetInt32OrDefault(this JsonElement element)
            => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value) ? value : default;
    }
}