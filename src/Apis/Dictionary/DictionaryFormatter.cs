﻿using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Discord;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Contains methods that help formatting dictionary entries into Markdown text that will be sent in a Discord embed.
/// </summary>
public static class DictionaryFormatter
{
    private static ReadOnlySpan<char> SuperscriptDigits => "\u2070\u00b9\u00b2\u00b3\u2074\u2075\u2076\u2077\u2078\u2079";

    private static ReadOnlySpan<char> SmallCapsChars => "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘꞯʀꜱᴛᴜᴠᴡxʏᴢ";

    /// <summary>
    /// Formats the title of an entry.
    /// </summary>
    /// <param name="entry">The dictionary entry.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatEntry(IDictionaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var builder = new StringBuilder($"\u200b**{entry.Entry}**\u200b");

        if (entry.Homograph is not null)
        {
            builder.Append(ToSuperscript(entry.Homograph));
        }

        builder.Append(' ');
        builder.AppendJoin(' ', entry.EntryVariants?.Select(x => FormatHtml(x)) ?? []);

        return builder.ToString();
    }

    /// <summary>
    /// Formats a part of speech block.
    /// </summary>
    /// <param name="block">The part of speech block.</param>
    /// <param name="entry">The parent entry.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatPartOfSpeechBlock(IDictionaryEntryBlock block, IDictionaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(block);

        var builder = new StringBuilder();

        if (!string.IsNullOrEmpty(entry.Pronunciation?.Ipa))
        {
            builder.Append(CultureInfo.InvariantCulture, $"/{FormatHtml(entry.Pronunciation.Ipa).Trim()}/");
        }
        else if (entry.Pronunciation?.Spell?.Count > 0)
        {
            builder.AppendJoin(' ', entry.Pronunciation.Spell.Select(x => $"[{FormatHtml(x).Trim()}]"));
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
                definition.Append(CultureInfo.InvariantCulture, $"\n  - ");

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

            if (builder.Length + definition.Length <= EmbedBuilder.MaxDescriptionLength)
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
            builder.Append(CultureInfo.InvariantCulture, $"\u200b**Origin of {entry.Entry}**\u200b");

            if (entry.Homograph is not null)
            {
                builder.Append(ToSuperscript(entry.Homograph));
            }

            builder.Append(CultureInfo.InvariantCulture, $"\n{FormatHtml(entry.Origin)}\n\n");
        }

        foreach (var note in entry.SupplementaryNotes ?? [])
        {
            builder.Append(CultureInfo.InvariantCulture, $"\u200b**{note.Type}**\u200b\n");
            builder.AppendJoin('\n', note.Content.Select(x => FormatHtml(x)));
            builder.Append("\n\n");
        }

        foreach (string spelling in entry.VariantSpellings ?? [])
        {
            builder.Append(FormatHtml(spelling));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string ToSuperscript(string digits)
    {
        return string.Create(digits.Length, digits, (span, state) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = SuperscriptDigits[state[i] - '0'];
            }
        });
    }

    private static string FormatHtml(string? htmlText, bool newLineExample = false, bool isSubdefinition = false)
    {
        if (string.IsNullOrEmpty(htmlText))
            return string.Empty;

        var parser = new HtmlParser();
        using var document = parser.ParseDocument(htmlText);

        if (document.Body!.ChildNodes.Length == 0)
        {
            return htmlText;
        }

        var builder = new StringBuilder();

        foreach (var element in document.Body!.ChildNodes)
        {
            if (element is IHtmlSpanElement span)
            {
                string className = span.ClassName ?? string.Empty;
                string content = span.TextContent;

                if (className is "luna-example italic" or "example italic" && newLineExample)
                {
                    builder.Append(isSubdefinition ? $"\n    -# > \u200b*{content}*\u200b" : $"\n> \u200b*{content}*\u200b");
                } // Sometimes there's text instead of numbers in a superscript class (e.g., satire)
                else if (className == "superscript" && content is [>= '0' and <= '9'])
                {
                    builder.Append(SuperscriptDigits[content[0] - '0']);
                }
                else if (className == "luna-wud small-caps")
                {
                    builder.Append(string.Create(content.Length, content, (converted, state) =>
                    {
                        for (int i = 0; i < converted.Length; i++)
                        {
                            converted[i] = state[i] is >= 'a' and <= 'z' ? SmallCapsChars[state[i] - 'a'] : state[i];
                        }
                    }));
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