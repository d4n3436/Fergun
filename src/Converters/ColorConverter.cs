﻿using System;
using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Color = System.Drawing.Color;

namespace Fergun.Converters;

/// <summary>
/// Represents a converter of <see cref="Color"/>.
/// </summary>
public class ColorConverter : TypeConverter<Color>
{
    /// <inheritdoc/>
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    /// <inheritdoc/>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        string value = option.Value as string ?? string.Empty;
        var color = Color.FromName(value);
        if (color.ToArgb() == 0)
        {
            var span = value.AsSpan()
                .Trim();

            int length = span.Length;

            span = span.TrimStart('#')
                .TrimStart("0x")
                .TrimStart("0X")
                .TrimStart("&h")
                .TrimStart("&H");

            if (TryParseInt32(span, span.Length < length, out int rawColor))
            {
                color = Color.FromArgb(rawColor);
            }
        }

        if (color.ToArgb() == 0)
        {
            var localizer = services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, localizer["CouldNotConvertValueToColor", value.Truncate(20)]));
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(color));
    }

    private static bool TryParseInt32(ReadOnlySpan<char> str, bool tryParseAsHexFirst, out int result)
    {
        var styles = tryParseAsHexFirst ? NumberStyles.HexNumber : NumberStyles.Integer;
        bool success = int.TryParse(str, styles, CultureInfo.InvariantCulture, out result);

        styles = tryParseAsHexFirst ? NumberStyles.Integer : NumberStyles.HexNumber;
        return success ? success : int.TryParse(str, styles, CultureInfo.InvariantCulture, out result);
    }
}