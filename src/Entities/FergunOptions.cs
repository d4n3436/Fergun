using System;
using System.Collections.Generic;
using System.ComponentModel;
using Discord;
using Fergun.Converters;
using Fergun.Interactive.Pagination;

namespace Fergun;

/// <summary>
/// Represents general Fergun settings.
/// </summary>
public class FergunOptions
{
    /// <summary>
    /// Returns the constant "Fergun".
    /// </summary>
    public const string Fergun = nameof(Fergun);

    /// <summary>
    /// Gets the support server URL.
    /// </summary>
    public Uri? SupportServerUrl { get; init; }

    /// <summary>
    /// Gets the default paginator timeout.
    /// </summary>
    public TimeSpan PaginatorTimeout { get; init; }

    /// <summary>
    /// Gets the default selection timeout.
    /// </summary>
    public TimeSpan SelectionTimeout { get; init; }

    /// <summary>
    /// Gets the dictionary of paginator emotes.
    /// </summary>
    [TypeConverter(typeof(EmoteConverter))]
    public IDictionary<PaginatorAction, IEmote> PaginatorEmotes { get; init; } = new Dictionary<PaginatorAction, IEmote>();

    /// <summary>
    /// Gets the extra emotes.
    /// </summary>
    public ExtraEmotes ExtraEmotes { get; init; } = new();
}