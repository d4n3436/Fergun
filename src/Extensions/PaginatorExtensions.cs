using System;
using System.Linq;
using Discord;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Localization;

namespace Fergun.Extensions;

public static class PaginatorExtensions
{
    private static readonly PaginatorAction[] _defaultActions =
    [
        PaginatorAction.Backward,
        PaginatorAction.Forward,
        PaginatorAction.Jump,
        PaginatorAction.Exit
    ];

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

        var buttons = options
            .PaginatorEmotes
            .Where(x => actions.Contains(x.Key))
            .OrderBy(x => Array.IndexOf(actions, x.Key))
            .Select(x => new PaginatorButton(x.Value, x.Key, x.Key == PaginatorAction.Exit ? ButtonStyle.Danger : ButtonStyle.Secondary));

        return builder.WithOptions(buttons);
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
            _ => throw new ArgumentException(localizer["UnknownPaginatorBuilderType"], nameof(builder))
        };

        builder.WithJumpInputPrompt(localizer["JumpInputPrompt"]);
        builder.WithJumpInputTextLabel(localizer["JumpInputTextLabel", 1, pageCount]);
        builder.WithInvalidJumpInputMessage(localizer["InvalidJumpInput", 1, pageCount]);
        builder.WithJumpInputInUseMessage(localizer["JumpInputInUse"]);
        builder.WithExpiredJumpInputMessage(localizer["ExpiredJumpInput", builder.JumpInputTimeout.TotalSeconds]);

        return (TBuilder)builder;
    }
}