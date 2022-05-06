using Discord;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Localization;
using System.Diagnostics.CodeAnalysis;

namespace Fergun.Extensions;

public static class PaginatorExtensions
{
    // We use this because the dictionary of emotes is not deserialized in the order it is provided.
    private static readonly PaginatorAction[] _orderedActions =
    {
        PaginatorAction.SkipToStart,
        PaginatorAction.Backward,
        PaginatorAction.Forward,
        PaginatorAction.SkipToEnd,
        PaginatorAction.Jump,
        PaginatorAction.Exit
    };

    /// <summary>
    /// Adds Fergun emotes.
    /// </summary>
    /// <typeparam name="TPaginator">The type of the paginator.</typeparam>
    /// <typeparam name="TBuilder">The type of the paginator builder.</typeparam>
    /// <param name="builder">A paginator builder.</param>
    /// <param name="options">The interactive options.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithFergunEmotes<TPaginator, TBuilder>(this PaginatorBuilder<TPaginator, TBuilder> builder, InteractiveOptions options)
        where TPaginator : Paginator
        where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        var emotes = options
            .PaginatorEmotes
            .OrderBy(x => Array.IndexOf(_orderedActions, x.Key))
            .Select(pair => (Success: pair.Value.TryParseEmote(out var emote), Emote: emote, Action: pair.Key))
            .Where(x => x.Success)
            .ToDictionary(x => x.Emote!, x => x.Action);

        return builder.WithOptions(emotes);
    }

    /// <summary>
    /// Sets the localized prompts.
    /// </summary>
    /// <typeparam name="TPaginator">The type of the paginator.</typeparam>
    /// <typeparam name="TBuilder">The type of the paginator builder.</typeparam>
    /// <param name="builder">The paginator builder.</param>
    /// <param name="localizer">The localizer.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithLocalizedPrompts<TPaginator, TBuilder>(this BaseLazyPaginatorBuilder<TPaginator, TBuilder> builder, IStringLocalizer localizer)
        where TPaginator : BaseLazyPaginator
        where TBuilder : BaseLazyPaginatorBuilder<TPaginator, TBuilder>
    {
        builder.WithJumpInputPrompt(localizer["Enter a page number"]);
        builder.WithJumpInputTextLabel(localizer["Page number ({0}-{1})", 1, builder.MaxPageIndex + 1]);
        builder.WithInvalidJumpInputMessage(localizer["Invalid input. The number must be in the range of {0} to {1}, excluding the current page.", 1, builder.MaxPageIndex + 1]);
        builder.WithJumpInputInUseMessage(localizer["Another user is currently using this action. Try again later."]);
        builder.WithExpiredJumpInputMessage(localizer["Expired modal interaction. You must respond within {0} seconds.", builder.JumpInputTimeout.TotalSeconds]);

        return (TBuilder)builder;
    }

    private static bool TryParseEmote(this string rawEmote, [MaybeNullWhen(false)] out IEmote emote)
    {
        bool success = Emote.TryParse(rawEmote, out var temp);
        emote = temp;

        if (!success)
        {
            success = Emoji.TryParse(rawEmote, out var temp2);
            emote = temp2;
        }

        return success;
    }
}