﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Converters;

/// <summary>
/// Represent a converter of <see cref="MicrosoftVoice"/>.
/// </summary>
public class MicrosoftVoiceConverter : TypeConverter<MicrosoftVoice>
{
    /// <inheritdoc/>
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    /// <inheritdoc/>
    public override async Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string value = option.Value as string ?? string.Empty;

        var translator = services
            .GetRequiredService<MicrosoftTranslator>();

        var voices = MicrosoftTranslator.DefaultVoices.Values;
        var task = translator.GetTTSVoicesAsync();
        if (task.IsCompletedSuccessfully)
        {
            voices = await task;
        }

        var voice = voices.FirstOrDefault(x => x.ShortName == value);

        if (voice is null)
        {
            var localizer = services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            return TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, localizer["UnableToGetVoice"]);
        }

        return TypeConverterResult.FromSuccess(voice);
    }
}