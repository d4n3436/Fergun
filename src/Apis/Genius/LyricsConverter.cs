using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;

namespace Fergun.Apis.Genius;

/// <summary>
/// Parses and formats the lyrics from a DOM object into markdown.
/// </summary>
public class LyricsConverter : JsonConverter<string>
{
    private const string BOLD = "b";
    private const string DFP_UNIT = "dfp-unit";
    private const string HORIZONTAL_LINE = "hr";
    private const string IMAGE = "img";
    private const string ITALIC = "i";
    private const string LINE_BREAK = "br";
    private const string LINK = "a";
    private const string UNDERLINE = "u";

    /// <inheritdoc/>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var builder = new StringBuilder();
        var dom = JsonElement.ParseValue(ref reader)
            .GetProperty("dom"u8);

        IterateContent(dom, builder);
        return builder.ToString();
    }

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Converter is only used for deserialization.")]
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => throw new NotSupportedException();

    private static void IterateContent(in JsonElement element, StringBuilder builder, bool escape = true)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            builder.Append(escape ? Format.Sanitize(value) : value);
        }
        else if (element.ValueKind == JsonValueKind.Object) // either tag or tag + children
        {
            string? tag = element.GetProperty("tag"u8).GetString();
            bool realLink = element.TryGetProperty("data"u8, out var data) && data.TryGetProperty("real-link"u8, out var realLinkProp) && realLinkProp.ValueEquals("true"u8);

            (string? markDownStart, string? markDownEnd) = tag switch
            {
                ITALIC => ("*", "*"),
                BOLD => ("**", "**"),
                LINE_BREAK or HORIZONTAL_LINE or IMAGE or DFP_UNIT => ("\n", null),
                LINK when realLink => ("[", $"]({element.GetProperty("attributes"u8).GetProperty("href"u8).GetString()})"),
                UNDERLINE => ("__", "__"),
                "h1" => ("\n# ", "\n"),
                "h2" => ("\n## ", "\n"),
                "h3" => ("\n### ", "\n"),
                _ => (null, null)
            };

            if (builder.Length > 0 && builder[^1] is '*' or '_')
            {
                builder.Append('\u200b'); // Append zero-width space to prevent markdown from breaking
            }

            builder.Append(markDownStart);

            if (element.TryGetProperty("children"u8, out var children))
            {
                Debug.Assert(children.ValueKind == JsonValueKind.Array, $"Expected {nameof(children)} to be an array.");

                foreach (var child in children.EnumerateArray())
                {
                    IterateContent(child, builder, !realLink);
                }

                builder.Append(markDownEnd);
            }
        }
    }
}