using System.Diagnostics.CodeAnalysis;
using Discord;
using GTranslate;

namespace Fergun.Extensions;

public static class InteractionExtensions
{
    public static async Task RespondWarningAsync(this IDiscordInteraction interaction, string message, bool ephemeral = false)
    {
        var embed = new EmbedBuilder()
            .WithDescription($"⚠ {message}")
            .WithColor(Color.Orange)
            .Build();

        await interaction.RespondAsync(embed: embed, ephemeral: ephemeral);
    }

    public static async Task FollowupWarning(this IDiscordInteraction interaction, string message, bool ephemeral = false)
    {
        var embed = new EmbedBuilder()
            .WithDescription($"⚠ {message}")
            .WithColor(Color.Orange)
            .Build();

        await interaction.FollowupAsync(embed: embed, ephemeral: ephemeral);
    }

    public static string GetTwoLetterLanguageCode(this IDiscordInteraction interaction, string defaultLanguage = "en")
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
        string lang = interaction.GetTwoLetterLanguageCode();
        return Language.TryGetLanguage(lang, out language);
    }

    public static string GetLocale(this IDiscordInteraction interaction, string defaultLocale = "en-US")
        => interaction.UserLocale ?? interaction.GuildLocale ?? defaultLocale;
}