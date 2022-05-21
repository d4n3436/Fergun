using System.Globalization;
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
                .TrimStart()
                .TrimStart('#')
                .TrimStart("0x")
                .TrimStart("0X")
                .TrimStart("&h")
                .TrimStart("&H");

            if ((uint.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint rawColor)
                 || uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rawColor))
                && rawColor <= Discord.Color.MaxDecimalValue)
            {
                color = Color.FromArgb((int)rawColor);
            }
        }

        if (color.ToArgb() == 0)
        {
            var localizer = services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, localizer["Could not convert \"{0}\" to a color.", value.Truncate(20)]));
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(color));
    }
}