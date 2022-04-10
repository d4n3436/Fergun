using System.Globalization;
using System.Text;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;

namespace Fergun.Modules;

[Group("urban", "Urban Dictionary commands")]
public class UrbanModule : InteractionModuleBase
{
    private readonly IFergunLocalizer<UrbanModule> _localizer;
    private readonly IUrbanDictionary _urbanDictionary;
    private readonly InteractiveService _interactive;

    public UrbanModule(IFergunLocalizer<UrbanModule> localizer, IUrbanDictionary urbanDictionary, InteractiveService interactive)
    {
        _localizer = localizer;
        _urbanDictionary = urbanDictionary;
        _interactive = interactive;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = new CultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("search", "Searches for definitions for a term in Urban Dictionary.")]
    public async Task Search([Autocomplete(typeof(UrbanAutocompleteHandler))] [Summary(description: "The term to search.")] string term)
        => await SearchAndSendAsync(UrbanSearchType.Search, term);

    [SlashCommand("random", "Gets random definitions from Urban Dictionary.")]
    public async Task Random() => await SearchAndSendAsync(UrbanSearchType.Random);

    [SlashCommand("words-of-the-day", "Gets the words of the day in Urban Dictionary.")]
    public async Task WordsOfTheDay() => await SearchAndSendAsync(UrbanSearchType.WordsOfTheDay);

    public async Task SearchAndSendAsync(UrbanSearchType searchType, string? term = null)
    {
        await DeferAsync();

        var definitions = searchType switch
        {
            UrbanSearchType.Search => await _urbanDictionary.GetDefinitionsAsync(term!),
            UrbanSearchType.Random => await _urbanDictionary.GetRandomDefinitionsAsync(),
            UrbanSearchType.WordsOfTheDay => await _urbanDictionary.GetWordsOfTheDayAsync(),
            _ => throw new ArgumentException("Invalid search type.", nameof(searchType))
        };

        if (definitions.Count == 0)
        {
            await Context.Interaction.FollowupWarning(_localizer["No results."]);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes()
            .WithActionOnCancellation(ActionOnStop.DisableInput)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithMaxPageIndex(definitions.Count - 1)
            .WithFooter(PaginatorFooter.None)
            .AddUser(Context.User)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10), InteractionResponseType.DeferredChannelMessageWithSource);

        PageBuilder GeneratePage(int i)
        {
            var description = new StringBuilder(definitions[i].Definition.Length + definitions[i].Example.Length);
            description.Append(Format.Sanitize(definitions[i].Definition));
            if (!string.IsNullOrEmpty(definitions[i].Example))
            {
                description.Append("\n\n");
                description.Append(Format.Italics(Format.Sanitize(definitions[i].Example.Trim())));
            }

            string footer = searchType switch
            {
                UrbanSearchType.Random => _localizer["Urban Dictionary (Random Definitions) | Page {0} of {1}", i + 1, definitions.Count],
                UrbanSearchType.WordsOfTheDay => _localizer["Urban Dictionary (Words of the day, {0}) | Page {1} of {2}", definitions[i].Date!, i + 1, definitions.Count],
                _ => _localizer["Urban Dictionary | Page {0} of {1}", i + 1, definitions.Count]
            };

            return new PageBuilder()
                .WithTitle(definitions[i].Word)
                .WithUrl(definitions[i].Permalink)
                .WithAuthor(_localizer["By {0}", definitions[i].Author], url: $"https://www.urbandictionary.com/author.php?author={Uri.EscapeDataString(definitions[i].Author)}")
                .WithDescription(description.ToString())
                .AddField("👍", definitions[i].ThumbsUp, true)
                .AddField("👎", definitions[i].ThumbsDown, true)
                .WithFooter(footer, Constants.UrbanDictionaryIconUrl)
                .WithTimestamp(definitions[i].WrittenOn)
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