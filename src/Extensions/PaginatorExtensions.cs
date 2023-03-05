using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Localization;

namespace Fergun.Extensions;

public static class PaginatorExtensions
{
    private static readonly PaginatorAction[] _defaultActions =
    {
        PaginatorAction.Backward,
        PaginatorAction.Forward,
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
    /// <param name="actions">The actions to add. If null. The default actions will be added.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithFergunEmotes<TPaginator, TBuilder>(this PaginatorBuilder<TPaginator, TBuilder> builder,
        FergunOptions options, PaginatorAction[]? actions = null)
        where TPaginator : Paginator
        where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        actions ??= _defaultActions;

        var emotes = options
            .PaginatorEmotes
            .OrderBy(x => Array.IndexOf(actions, x.Key))
            .Where(x => actions.Contains(x.Key))
            .ToDictionary(x => x.Value, x => x.Key);

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
    public static TBuilder WithLocalizedPrompts<TPaginator, TBuilder>(this PaginatorBuilder<TPaginator, TBuilder> builder, IStringLocalizer localizer)
        where TPaginator : Paginator
        where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        int pageCount = builder switch
        {
            LazyPaginatorBuilder lazy => lazy.MaxPageIndex + 1,
            StaticPaginatorBuilder @static => @static.Pages.Count,
            _ => throw new ArgumentException("Unknwon paginator builder type", nameof(builder))
        };

        builder.WithJumpInputPrompt(localizer["Enter a page number"]);
        builder.WithJumpInputTextLabel(localizer["Page number ({0}-{1})", 1, pageCount]);
        builder.WithInvalidJumpInputMessage(localizer["Invalid input. The number must be in the range of {0} to {1}, excluding the current page.", 1, pageCount]);
        builder.WithJumpInputInUseMessage(localizer["Another user is currently using this action. Try again later."]);
        builder.WithExpiredJumpInputMessage(localizer["Expired modal interaction. You must respond within {0} seconds.", builder.JumpInputTimeout.TotalSeconds]);

        return (TBuilder)builder;
    }
}