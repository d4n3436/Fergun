using System.Linq;
using Discord;
using Fergun.Interactive;
using Fergun.Interactive.Selection;
using Microsoft.Extensions.Localization;

namespace Fergun.Extensions;

public static class SelectionExtensions
{
    /// <summary>
    /// Sets the localized prompts.
    /// </summary>
    /// <typeparam name="TSelection">The type of the built selection.</typeparam>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    /// <typeparam name="TBuilder">The type of this builder.</typeparam>
    /// <param name="builder">The selection builder.</param>
    /// <param name="localizer">The localizer.</param>
    /// <returns>This builder.</returns>
    public static TBuilder WithLocalizedPrompts<TSelection, TOption, TBuilder>(this BaseSelectionBuilder<TSelection, TOption, TBuilder> builder, IStringLocalizer localizer)
        where TSelection : BaseSelection<TOption>
        where TBuilder : BaseSelectionBuilder<TSelection, TOption, TBuilder>
    {
        builder.WithRestrictedPageFactory(users => new PageBuilder().WithDescription(localizer["RestrictedSelectionInputMessage", users.First().Mention]).WithColor(Constants.DefaultColor).Build());

        return (TBuilder)builder;
    }
}