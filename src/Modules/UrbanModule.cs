using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Humanizer;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[Group("urban", "Urban Dictionary commands")]
public class UrbanModule : InteractionModuleBase
{
    private readonly IFergunLocalizer<UrbanModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly IUrbanDictionary _urbanDictionary;
    private readonly InteractiveService _interactive;

    public UrbanModule(IFergunLocalizer<UrbanModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        IUrbanDictionary urbanDictionary, InteractiveService interactive)
    {
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _urbanDictionary = urbanDictionary;
        _interactive = interactive;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("search", "Searches for definitions for a term in Urban Dictionary.")]
    public async Task<RuntimeResult> SearchAsync([Autocomplete(typeof(UrbanAutocompleteHandler))] [Summary(description: "The term to search.")] string term)
        => await SearchAndSendAsync(UrbanSearchType.Search, term);

    [SlashCommand("random", "Gets random definitions from Urban Dictionary.")]
    public async Task<RuntimeResult> RandomAsync() => await SearchAndSendAsync(UrbanSearchType.Random);

    [SlashCommand("words-of-the-day", "Gets the words of the day in Urban Dictionary.")]
    public async Task<RuntimeResult> WordsOfTheDayAsync() => await SearchAndSendAsync(UrbanSearchType.WordsOfTheDay);

    public async Task<RuntimeResult> SearchAndSendAsync(UrbanSearchType searchType, string? term = null)
    {
        await Context.Interaction.DeferAsync();

        var definitions = searchType switch
        {
            UrbanSearchType.Search => await _urbanDictionary.GetDefinitionsAsync(term!),
            UrbanSearchType.Random => await _urbanDictionary.GetRandomDefinitionsAsync(),
            UrbanSearchType.WordsOfTheDay => await _urbanDictionary.GetWordsOfTheDayAsync(),
            _ => throw new ArgumentException(_localizer["Invalid search type."], nameof(searchType))
        };

        if (definitions.Count == 0)
        {
            return FergunResult.FromError(_localizer["No results."]);
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_fergunOptions)
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(definitions.Count - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int i)
        {
            var definition = definitions[i];

            var description = new StringBuilder(definition.Definition.Length + definition.Example.Length);
            description.Append(Format.Sanitize(definition.Definition));
            if (!string.IsNullOrEmpty(definition.Example))
            {
                description.Append("\n\n");
                description.Append(Format.Italics(Format.Sanitize(definition.Example.Trim())));
            }

            string footer = searchType switch
            {
                UrbanSearchType.Random => _localizer["Urban Dictionary (Random Definitions) | Page {0} of {1}", i + 1, definitions.Count],
                UrbanSearchType.WordsOfTheDay => _localizer["Urban Dictionary (Words of the day, {0}) | Page {1} of {2}", definition.Date!, i + 1, definitions.Count],
                _ => _localizer["Urban Dictionary | Page {0} of {1}", i + 1, definitions.Count]
            };

            return new PageBuilder()
                .WithTitle(definition.Word)
                .WithUrl($"https://www.urbandictionary.com/urbanup.php?path=%2F{definition.Id}")
                .WithAuthor(_localizer["By {0}", definition.Author], url: $"https://www.urbandictionary.com/author.php?author={Uri.EscapeDataString(definition.Author)}")
                .WithDescription(description.ToString().Truncate(EmbedBuilder.MaxDescriptionLength))
                .AddField("👍", definition.ThumbsUp, true)
                .AddField("👎", definition.ThumbsDown, true)
                .WithFooter(footer, Constants.UrbanDictionaryIconUrl)
                .WithTimestamp(definition.WrittenOn)
                .WithColor(Color.Orange); // 0x10151BU 0x1B2936U
        }
    }

    public enum UrbanSearchType
    {
        Search,
        Random,
        WordsOfTheDay
    }
}