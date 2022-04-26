using Discord;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Localization;

namespace Fergun.Extensions;

public static class PaginatorExtensions
{
    /// <summary>
    /// Adds Fergun emotes.
    /// </summary>
    /// <typeparam name="TPaginator">The type of the paginator.</typeparam>
    /// <typeparam name="TBuilder">The type of the paginator builder.</typeparam>
    /// <param name="builder">A paginator builder.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithFergunEmotes<TPaginator, TBuilder>(this PaginatorBuilder<TPaginator, TBuilder> builder)
        where TPaginator : Paginator
        where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
    {
        builder.Options.Clear();

        builder.AddOption(Emoji.Parse("◀️"), PaginatorAction.Backward);
        builder.AddOption(Emoji.Parse("▶️"), PaginatorAction.Forward);
        builder.AddOption(Emoji.Parse("🔢"), PaginatorAction.Jump);
        builder.AddOption(Emoji.Parse("🛑"), PaginatorAction.Exit);

        return (TBuilder)builder;
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
}