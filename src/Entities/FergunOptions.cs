using Discord;
using Fergun.Converters;
using Fergun.Interactive.Pagination;
using System.ComponentModel;

namespace Fergun;

/// <summary>
/// Represents general Fergun settings.
/// </summary>
public class FergunOptions
{
    public const string Fergun = nameof(Fergun);

    /// <summary>
    /// Gets or sets the support server URL.
    /// </summary>
    public Uri? SupportServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the default paginator timeout.
    /// </summary>
    public TimeSpan PaginatorTimeout { get; set; }

    /// <summary>
    /// Gets or sets the default selection timeout.
    /// </summary>
    public TimeSpan SelectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of paginator emotes.
    /// </summary>
    [TypeConverter(typeof(EmoteConverter))]
    public IDictionary<PaginatorAction, IEmote> PaginatorEmotes { get; set; } = new Dictionary<PaginatorAction, IEmote>();

    /// <summary>
    /// Gets or sets the extra emotes.
    /// </summary>
    public ExtraEmotes ExtraEmotes { get; set; } = new();
}

/// <summary>
/// Contains extra emotes used in Fergun.
/// </summary>
public class ExtraEmotes
{
    /// <summary>
    /// Gets the info emote.
    /// </summary>
    [TypeConverter(typeof(EmoteConverter))]
    public IEmote InfoEmote { get; set; } = null!;
}