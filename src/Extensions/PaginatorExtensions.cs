using System;
using System.Linq;
using Discord;
using Fergun.Common;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Services;
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

    /// <param name="builder">A paginator builder.</param>
    /// <typeparam name="TPaginator">The type of the paginator.</typeparam>
    /// <typeparam name="TBuilder">The type of the paginator builder.</typeparam>
    extension<TPaginator, TBuilder>(PaginatorBuilder<TPaginator, TBuilder> builder) where TPaginator : Paginator where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        /// <summary>
        /// Adds Fergun emotes.
        /// </summary>
        /// <param name="emotes">The emote provider.</param>
        /// <param name="actions">The actions to add. If null. The default actions will be added.</param>
        /// <returns>This builder.</returns>
        public TBuilder WithFergunEmotes(FergunEmoteProvider emotes, PaginatorAction[]? actions = null)
        {
            actions ??= _defaultActions;

            var buttons = actions
                .Select(action => new PaginatorButton(
                    emotes.GetEmote(action),
                    action,
                    action == PaginatorAction.Exit ? ButtonStyle.Danger : ButtonStyle.Secondary
                ));

            return builder.WithOptions(buttons);
        }

        /// <summary>
        /// Sets the localized prompts.
        /// </summary>
        /// <param name="localizer">The localizer.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when the builder type is unknown.</exception>
        public TBuilder WithLocalizedPrompts(IStringLocalizer localizer)
        {
            int pageCount = builder switch
            {
                LazyPaginatorBuilder lazy => lazy.MaxPageIndex + 1,
                StaticPaginatorBuilder @static => @static.Pages.Count,
                _ => throw new ArgumentException(localizer["UnknownPaginatorBuilderType"], nameof(builder))
            };

            builder.WithRestrictedPageFactory(users => new PageBuilder().WithDescription(localizer["RestrictedPaginatorInputMessage", users.First().Mention]).WithColor(Constants.DefaultColor).Build());
            builder.WithJumpInputPrompt(localizer["JumpInputPrompt"]);
            builder.WithJumpInputTextLabel(localizer["JumpInputTextLabel", 1, pageCount]);
            builder.WithInvalidJumpInputMessage(localizer["InvalidJumpInput", 1, pageCount]);
            builder.WithJumpInputInUseMessage(localizer["JumpInputInUse"]);
            builder.WithExpiredJumpInputMessage(localizer["ExpiredJumpInput", builder.JumpInputTimeout.TotalSeconds]);

            return (TBuilder)builder;
        }
    }
}