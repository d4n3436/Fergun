using System.Diagnostics.CodeAnalysis;
using Discord;
using GTranslate;

namespace Fergun.Extensions;

public static class InteractionExtensions
{
    public static string GetLanguageCode(this IDiscordInteraction interaction, string defaultLanguage = "en")
    {
        string language = interaction.UserLocale ?? interaction.GuildLocale;
        if (string.IsNullOrEmpty(language))
            return defaultLanguage;

        int index = language.IndexOf('-');
        if (index != -1)
        {
            language = language[..index];
        }

        return language;
    }

    public static bool TryGetLanguage(this IDiscordInteraction interaction, [MaybeNullWhen(false)] out Language language)
    {
        return Language.TryGetLanguage(interaction.GetLocale(), out language) ||
               Language.TryGetLanguage(interaction.GetLanguageCode(), out language);
    }

    public static string GetLocale(this IDiscordInteraction interaction, string defaultLocale = "en-US")
        => interaction.UserLocale ?? interaction.GuildLocale ?? defaultLocale;
}