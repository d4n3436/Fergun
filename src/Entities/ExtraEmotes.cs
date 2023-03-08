using System.ComponentModel;
using Discord;
using Fergun.Converters;

namespace Fergun;

/// <summary>
/// Contains extra emotes used in Fergun.
/// </summary>
public class ExtraEmotes
{
    /// <summary>
    /// Gets the info emote.
    /// </summary>
    [TypeConverter(typeof(EmoteConverter))]
    public IEmote InfoEmote { get; init; } = null!;
}