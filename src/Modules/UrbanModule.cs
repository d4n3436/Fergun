using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Urban;
using Fergun.Configuration;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Modules.Handlers;
using Fergun.Preconditions;
using Fergun.Services;
using Humanizer;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
[Ratelimit(2, Constants.GlobalRatelimitPeriod)]
[Group("urban", "Urban Dictionary commands")]
public class UrbanModule : InteractionModuleBase
{
    private readonly ILogger<UrbanModule> _logger;
    private readonly IFergunLocalizer<UrbanModule> _localizer;
    private readonly FergunOptions _fergunOptions;
    private readonly FergunEmoteProvider _emotes;
    private readonly IUrbanDictionaryClient _urbanDictionary;
    private readonly InteractiveService _interactive;

    public UrbanModule(ILogger<UrbanModule> logger, IFergunLocalizer<UrbanModule> localizer, IOptionsSnapshot<FergunOptions> fergunOptions,
        FergunEmoteProvider emotes, IUrbanDictionaryClient urbanDictionary, InteractiveService interactive)
    {
        _logger = logger;
        _localizer = localizer;
        _fergunOptions = fergunOptions.Value;
        _emotes = emotes;
        _urbanDictionary = urbanDictionary;
        _interactive = interactive;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("search", "Searches for definitions for a term in Urban Dictionary.")]
    public async Task<RuntimeResult> SearchAsync([Autocomplete(typeof(UrbanAutocompleteHandler))][Summary(description: "The term to search.")] string term)
        => await SearchAndSendAsync(UrbanSearchType.Search, term);

    [SlashCommand("random", "Gets random definitions from Urban Dictionary.")]
    public async Task<RuntimeResult> RandomAsync() => await SearchAndSendAsync(UrbanSearchType.Random);

    [SlashCommand("words-of-the-day", "Gets the words of the day in Urban Dictionary.")]
    public async Task<RuntimeResult> WordsOfTheDayAsync() => await SearchAndSendAsync(UrbanSearchType.WordsOfTheDay);

    public async Task<RuntimeResult> SearchAndSendAsync(UrbanSearchType searchType, string? term = null)
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Sending Urban Dictionary search request (type: {Type}, term: {Term})", searchType, term ?? "(None)");

        var definitions = searchType switch
        {
            UrbanSearchType.Search => await _urbanDictionary.GetDefinitionsAsync(term!),
            UrbanSearchType.Random => await _urbanDictionary.GetRandomDefinitionsAsync(),
            UrbanSearchType.WordsOfTheDay => await _urbanDictionary.GetWordsOfTheDayAsync(),
            _ => throw new ArgumentException(_localizer["InvalidSearchType"], nameof(searchType))
        };

        _logger.LogDebug("Definition count: {Count}", definitions.Count);

        if (definitions.Count == 0)
        {
            return FergunResult.FromError(_localizer["NoResults"]);
        }

        var paginator = new LazyPaginatorBuilder()
            .WithPageFactory(GeneratePage)
            .WithFergunEmotes(_emotes)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
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
            if (definition.Example.Length > 0)
            {
                description.Append("\n\n")
                    .Append(Format.Italics(Format.Sanitize(definition.Example.Trim())));
            }

            string footer = searchType switch
            {
                UrbanSearchType.Search => _localizer["UrbanPaginatorFooter", i + 1, definitions.Count],
                UrbanSearchType.Random => _localizer["UrbanRandomPaginatorFooter", i + 1, definitions.Count],
                UrbanSearchType.WordsOfTheDay => _localizer["UrbanWordsOfTheDayPaginatorFooter", definition.Date!, i + 1, definitions.Count],
                _ => throw new UnreachableException()
            };

            return new PageBuilder()
                .WithTitle(definition.Word)
                .WithUrl($"https://www.urbandictionary.com/urbanup.php?path=%2F{definition.Id}")
                .WithAuthor(_localizer["ByAuthor", definition.Author], url: $"https://www.urbandictionary.com/author.php?author={Uri.EscapeDataString(definition.Author)}")
                .WithDescription(description.ToString().Truncate(EmbedBuilder.MaxDescriptionLength))
                .AddField("👍", definition.ThumbsUp, true)
                .AddField("👎", definition.ThumbsDown, true)
                .WithFooter(footer, Constants.UrbanDictionaryIconUrl)
                .WithTimestamp(definition.WrittenOn)
                .WithColor(Color.Orange); // 0x10151BU 0x1B2936U
        }
    }
}