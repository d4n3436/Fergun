using System;
using System.ComponentModel;
using System.Globalization;
using Discord;

namespace Fergun.Converters;

/// <summary>
/// Converts a <see cref="string"/> into an <see cref="IEmote"/>.
/// </summary>
public class EmoteConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string str)
            throw GetConvertFromException(value);

        bool success = Emote.TryParse(str, out var temp);
        IEmote emote = temp;

        if (!success)
        {
            emote = Emoji.Parse(str);
        }

        return emote;

    }
}