using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Discord;
using Fergun.Interactive.Pagination;

namespace Fergun.Services;

/// <summary>
/// Provide emotes for use in Discord commands.
/// </summary>
public class FergunEmoteProvider
{
    private const string GoogleLensIconEmoteName = "google_lens_icon";

    private const string BingIconEmoteName = "bing_icon";

    private const string YandexIconEmoteName = "yandex_icon";

    private const string DictionaryComIconEmoteName = "dictionary_com_icon";

    private const string SkipToStartEmoteName = "skip_to_start";

    private const string BackwardEmoteName = "backward";

    private const string ForwardEmoteName = "forward";

    private const string SkipToEndEmoteName = "skip_to_end";

    private const string JumpEmoteName = "jump";

    private const string ExitEmoteName = "exit";

    private const string InfoEmoteName = "info";

    private static readonly IEmote _defaultSkipToStartEmote = new Emoji("⏮");

    private static readonly IEmote _defaultBackwardEmote = new Emoji("◀️");

    private static readonly IEmote _defaultForwardEmote = new Emoji("▶️");

    private static readonly IEmote _defaultSkipToEndEmote = new Emoji("⏭");

    private static readonly IEmote _defaultJumpEmote = new Emoji("🔢");

    private static readonly IEmote _defaultExitEmote = new Emoji("🛑");

    private static readonly IEmote _defaultInfoEmote = new Emoji("ℹ️");

    /// <summary>
    /// Gets the emote representing the Google Lens icon.
    /// </summary>
    public IEmote? GoogleLensIconEmote { get; private set; }

    /// <summary>
    /// Gets the emote representing the Bing icon.
    /// </summary>
    public IEmote? BingIconEmote { get; private set; }

    /// <summary>
    /// Gets the emote representing the Yandex icon.
    /// </summary>
    public IEmote? YandexIconEmote { get; private set; }

    /// <summary>
    /// Gets the emote representing the Dictionary.com icon.
    /// </summary>
    public IEmote? DictionaryComIconEmote { get; private set; }

    /// <summary>
    /// Gets the emote representing the button used to skip to the first paginator page. Defaults to the rewind emoji (⏮).
    /// </summary>
    public IEmote SkipToStartEmote { get; private set; } = _defaultSkipToStartEmote;

    /// <summary>
    /// Gets the emote representing the button used to go the previous paginator page. Defaults to the left arrow emoji (◀️).
    /// </summary>
    public IEmote BackwardEmote { get; private set; } = _defaultBackwardEmote;

    /// <summary>
    /// Gets the emote representing the button used to go the next paginator page. Defaults to the right arrow emoji (▶️).
    /// </summary>
    public IEmote ForwardEmote { get; private set; } = _defaultForwardEmote;

    /// <summary>
    /// Gets the emote representing the button used to skip to the last paginator page. Defaults to the fast-forward emoji (⏭).
    /// </summary>
    public IEmote SkipToEndEmote { get; private set; } = _defaultSkipToEndEmote;

    /// <summary>
    /// Gets the emote representing the button used to jump to a specific paginator page. Defaults to the input numbers emoji (🔢).
    /// </summary>
    public IEmote JumpEmote { get; private set; } = _defaultJumpEmote;

    /// <summary>
    /// Gets the emote representing the button used to exit the paginator or selection. Defaults to the stop sign emoji (🛑).
    /// </summary>
    public IEmote ExitEmote { get; private set; } = _defaultExitEmote;

    /// <summary>
    /// Gets the emote representing the button used to show more information about the displayed content. Defaults to the information emoji (ℹ️).
    /// </summary>
    public IEmote InfoEmote { get; private set; } = _defaultInfoEmote;

    /// <summary>
    /// Sets the emotes for use in this provider.
    /// </summary>
    /// <param name="emotes">A read-only collection of emotes.</param>
    public void SetEmotes(IReadOnlyCollection<IEmote> emotes)
    {
        ArgumentNullException.ThrowIfNull(emotes);

        GoogleLensIconEmote = emotes.FirstOrDefault(x => x.Name == GoogleLensIconEmoteName);
        BingIconEmote = emotes.FirstOrDefault(x => x.Name == BingIconEmoteName);
        YandexIconEmote = emotes.FirstOrDefault(x => x.Name == YandexIconEmoteName);
        DictionaryComIconEmote = emotes.FirstOrDefault(x => x.Name == DictionaryComIconEmoteName);

        SkipToStartEmote = emotes.FirstOrDefault(x => x.Name == SkipToStartEmoteName) ?? _defaultSkipToStartEmote;
        BackwardEmote = emotes.FirstOrDefault(x => x.Name == BackwardEmoteName) ?? _defaultBackwardEmote;
        ForwardEmote = emotes.FirstOrDefault(x => x.Name == ForwardEmoteName) ?? _defaultForwardEmote;
        SkipToEndEmote = emotes.FirstOrDefault(x => x.Name == SkipToEndEmoteName) ?? _defaultSkipToEndEmote;
        JumpEmote = emotes.FirstOrDefault(x => x.Name == JumpEmoteName) ?? _defaultJumpEmote;
        ExitEmote = emotes.FirstOrDefault(x => x.Name == ExitEmoteName) ?? _defaultExitEmote;
        InfoEmote = emotes.FirstOrDefault(x => x.Name == InfoEmoteName) ?? _defaultInfoEmote;
    }

    /// <summary>
    /// Gets the emote corresponding to the specified paginator action.
    /// </summary>
    /// <param name="action">The paginator action.</param>
    /// <returns>The emote.</returns>
    /// <exception cref="UnreachableException">Never thrown.</exception>
    public IEmote GetEmote(PaginatorAction action)
        => action switch
        {
            PaginatorAction.SkipToStart => SkipToStartEmote,
            PaginatorAction.Backward => BackwardEmote,
            PaginatorAction.Forward => ForwardEmote,
            PaginatorAction.SkipToEnd => SkipToEndEmote,
            PaginatorAction.Jump => JumpEmote,
            PaginatorAction.Exit => ExitEmote,
            _ => throw new UnreachableException()
        };
}