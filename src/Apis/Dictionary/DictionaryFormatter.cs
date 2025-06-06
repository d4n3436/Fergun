﻿using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Contains methods that help formatting dictionary entries into Markdown text that will be sent in Discord.
/// </summary>
public static class DictionaryFormatter
{
    private static ReadOnlySpan<char> SuperscriptDigits => "\u2070\u00b9\u00b2\u00b3\u2074\u2075\u2076\u2077\u2078\u2079";

    /// <summary>
    /// Formats the title of an entry.
    /// </summary>
    /// <param name="entry">The dictionary entry.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatEntry(IDictionaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var builder = new StringBuilder($"## \u200b**{entry.Entry}**\u200b");

        if (entry.Homograph is not null)
        {
            builder.Append(ToSuperscript(entry.Homograph));
        }

        return builder.Append(' ')
            .AppendJoin(' ', entry.EntryVariants?.Select(x => FormatHtml(x)) ?? [])
            .ToString();
    }

    /// <summary>
    /// Formats a part of speech block.
    /// </summary>
    /// <param name="block">The part of speech block.</param>
    /// <param name="entry">The parent entry.</param>
    /// <param name="maxLength">The max. length of the formatted text.</param>
    /// <returns>The formatted text.</returns>
    /// <exception cref="ArgumentException">Thrown when neither <see cref="IDictionaryDefinition.Ordinal"/> nor <see cref="IDictionaryDefinition.Order"/> are present.</exception>
    public static string FormatPartOfSpeechBlock(IDictionaryEntryBlock block, IDictionaryEntry entry, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(entry);

        var builder = new StringBuilder();

        if (!string.IsNullOrEmpty(entry.Pronunciation?.Ipa))
        {
            builder.Append(CultureInfo.InvariantCulture, $"/{FormatHtml(entry.Pronunciation.Ipa).Trim()}/");
        }

        builder.Append('\n');

        if (!string.IsNullOrEmpty(block.PartOfSpeech))
        {
            builder.Append(FormatHtml(block.PartOfSpeech));

            if (!string.IsNullOrEmpty(block.SupplementaryInfo))
            {
                builder.Append(CultureInfo.InvariantCulture, $" {FormatHtml(block.SupplementaryInfo)}");
            }

            builder.Append("\n\n");
        }

        foreach (var def in block.Definitions)
        {
            int order = def.Ordinal ?? def.Order ?? throw new ArgumentException($"Neither Ordinal nor Order were present on definition of \"{entry.Entry}\".");

            var definition = new StringBuilder($"{order}. ");
            if (!string.IsNullOrEmpty(def.PredefinitionContent))
            {
                definition.Append(CultureInfo.InvariantCulture, $"{FormatHtml(def.PredefinitionContent)} ");
            }

            definition.Append(def.Definition is null ? '\u200b' : FormatHtml(def.Definition, true)); // null on groups

            if (!string.IsNullOrEmpty(def.PostdefinitionContent))
            {
                definition.Append(CultureInfo.InvariantCulture, $" {FormatHtml(def.PostdefinitionContent, true)}"); // collins puts examples here while luna puts it inside the definition
            }

            foreach (var subDefinition in def.Subdefinitions)
            {
                definition.Append("\n  - ");

                if (!string.IsNullOrEmpty(subDefinition.PredefinitionContent))
                {
                    definition.Append(CultureInfo.InvariantCulture, $"{FormatHtml(subDefinition.PredefinitionContent)} ");
                }

                definition.Append(FormatHtml(subDefinition.Definition, true, true));

                if (!string.IsNullOrEmpty(subDefinition.PostdefinitionContent))
                {
                    definition.Append(CultureInfo.InvariantCulture, $" {FormatHtml(subDefinition.PostdefinitionContent, true, true)}");
                }
            }

            definition.Append("\n\n");

            if (builder.Length + definition.Length <= maxLength)
            {
                builder.Append(definition);
            }
            else
            {
                break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats the extra information of a dictionary entry.
    /// </summary>
    /// <param name="entry">The dictionary entry.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatExtraInformation(IDictionaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var builder = new StringBuilder();

        if (!string.IsNullOrEmpty(entry.Origin))
        {
            builder.Append(CultureInfo.InvariantCulture, $"### \u200b**Origin of {entry.Entry}**\u200b");

            if (entry.Homograph is not null)
            {
                builder.Append(ToSuperscript(entry.Homograph));
            }

            builder.Append(CultureInfo.InvariantCulture, $"\n{FormatHtml(entry.Origin)}\n\n");
        }

        return builder.ToString();
    }

    private static string ToSuperscript(string digits)
        => string.Create(digits.Length, digits, (span, state) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = SuperscriptDigits[state[i] - '0'];
            }
        });

    private static string FormatHtml(string? htmlText, bool newLineExample = false, bool isSubDefinition = false)
    {
        if (string.IsNullOrEmpty(htmlText))
            return string.Empty;

        var parser = new HtmlParser();
        using var document = parser.ParseDocument(htmlText);
        var builder = new StringBuilder();

        foreach (var element in document.Body!.ChildNodes)
        {
            if (element is IHtmlSpanElement span)
            {
                string className = span.ClassName ?? string.Empty;
                string content = span.TextContent;

                if (className is "luna-example italic" or "example italic" && newLineExample)
                {
                    builder.Append(isSubDefinition ? $"\n    -# > \u200b*{content}*\u200b" : $"\n> \u200b*{content}*\u200b");
                }
                else if (className.EndsWith("italic", StringComparison.Ordinal) || className.EndsWith("pos", StringComparison.Ordinal))
                {
                    builder.Append(CultureInfo.InvariantCulture, $"\u200b*{content}*\u200b");
                }
                else if (className.EndsWith("bold", StringComparison.Ordinal))
                {
                    builder.Append(CultureInfo.InvariantCulture, $"\u200b**{content}**\u200b");
                }
                else
                {
                    builder.Append(content);
                }
            }
            else if (element is IHtmlAnchorElement anchor && !string.IsNullOrEmpty(anchor.Text))
            {
                // This currently won't handle nested tags like <a href="/browse/back" class="luna-xref" data-linkid="nn1ov4">back<sup>2</sup> (def. 7)</a>.
                builder.Append(CultureInfo.InvariantCulture, $"[{anchor.Text}](https://dictionary.com{anchor.GetAttribute("href")})");
            }
            else
            {
                builder.Append(element.TextContent);
            }
        }

        return builder.ToString();
    }
}