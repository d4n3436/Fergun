using System.Diagnostics.CodeAnalysis;
using Discord;
using GTranslate;

namespace Fergun.Extensions;

public static class InteractionExtensions
{
    extension(IDiscordInteraction interaction)
    {
        public string GetLanguageCode(string defaultLanguage = "en")
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

        public bool TryGetLanguage([MaybeNullWhen(false)] out Language language)
            => Language.TryGetLanguage(interaction.GetLocale(), out language) ||
               Language.TryGetLanguage(interaction.GetLanguageCode(), out language);

        public string GetLocale(string defaultLocale = "en-US")
            => interaction.UserLocale ?? interaction.GuildLocale ?? defaultLocale;
    }
}