using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.APIs;
using Fergun.APIs.AIDungeon;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Selection;
using Fergun.Services;
using GTranslate.Translators;

namespace Fergun.Modules
{
    using ActionType = APIs.AIDungeon.ActionType;

    [Order(4)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Name("AIDungeon"), Group("aid"), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class AIDungeon : FergunBase
    {
        private static AiDungeonApi _api;
        private static readonly ConcurrentDictionary<uint, SemaphoreSlim> _queue = new ConcurrentDictionary<uint, SemaphoreSlim>();
        private static IReadOnlyDictionary<string, string> _modes;
        private static readonly Translator _translator = new Translator();

        private readonly CommandService _cmdService;
        private readonly LogService _logService;
        private readonly MessageCacheService _messageCache;
        private readonly InteractiveService _interactive;

        public AIDungeon(CommandService commands, LogService logService, MessageCacheService messageCache, InteractiveService interactive)
        {
            _api ??= new AiDungeonApi(new HttpClient { Timeout = TimeSpan.FromMinutes(1) }, FergunClient.Config.AiDungeonToken ?? "");
            _cmdService = commands;
            _logService = logService;
            _messageCache = messageCache;
            _interactive = interactive;
        }

        [Command("info")]
        [Summary("aidinfoSummary")]
        public async Task<RuntimeResult> Info()
        {
            var builder = new EmbedBuilder()
                .WithTitle(Locate("AIDHelp"))
                .AddField(Locate("AboutAIDTitle"), Locate("AboutAIDText"))
                .AddField(Locate("AIDHowToPlayTitle"), Locate("AIDHowToPlayText"))
                .AddField(Locate("InputTypes"), Locate("InputTypesList"));

            var aidCommands = _cmdService.Modules.FirstOrDefault(x => x.Name == "AIDungeon");
            if (aidCommands == null)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            var list = new StringBuilder();
            foreach (var command in aidCommands.Commands)
            {
                list.Append($"`{command.Name}");
                foreach (var parameter in command.Parameters)
                {
                    list.Append(' ');
                    list.Append(parameter.IsOptional ? '[' : '<');
                    list.Append(parameter.Name);
                    if (parameter.IsRemainder || parameter.IsMultiple)
                    {
                        list.Append("...");
                    }

                    list.Append(parameter.IsOptional ? ']' : '>');
                }
                list.Append($"`: {Locate(command.Summary)}\n\n");
            }

            builder.AddField(Locate("Commands"), list.ToString())
                .WithFooter(Locate("HelpFooter2"), Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("new", RunMode = RunMode.Async), Ratelimit(1, 1, Measure.Minutes)]
        [Summary("aidnewSummary")]
        [Alias("create")]
        public async Task<RuntimeResult> New()
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            if (_modes == null)
            {
                await Context.Channel.TriggerTypingAsync();
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "New: Downloading the mode list..."));

                AiDungeonScenario scenario;
                try
                {
                    scenario = await _api.GetScenarioAsync(AiDungeonApi.AllScenariosId);
                }
                catch (Exception e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception requesting scenario", e));
                    return FergunResult.FromError(e.Message);
                }

                if (scenario.Options.Count == 0)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The mode list is empty."));
                    return FergunResult.FromError(Locate("ErrorInAPI"));
                }

                _modes = scenario
                    .Options
                    .GroupBy(x => x.Title.Truncate(100), StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToDictionary(x => x.Title.Truncate(100), x => x.PublicId.ToString());
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "New: Using cached mode list..."));
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(Locate("AIDungeonWelcome"))
                .WithDescription($"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}")
                .WithThumbnailUrl(Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            var warningBuilder = new EmbedBuilder()
                .WithColor(FergunClient.Config.EmbedColor)
                .WithDescription($"\u26a0 {Locate("ReplyTimeout")} {Locate("CreationCanceled")}");

            var selectionBuilder = new SelectionBuilder<int>()
                .AddUser(Context.User)
                .WithInputType(InputType.SelectMenus)
                .WithOptions(Enumerable.Range(0, _modes.Count).ToArray())
                .WithStringConverter(x => _modes.ElementAt(x).Key.ToTitleCase())
                .WithSelectionPage(PageBuilder.FromEmbedBuilder(builder))
                .WithTimeoutPage(PageBuilder.FromEmbedBuilder(warningBuilder))
                .WithActionOnTimeout(ActionOnStop.ModifyMessage | ActionOnStop.DisableInput);

#if !DNETLABS
            for (int i = 0; i < _modes.Count; i++)
            {
                builder.Description += $"\n**{i + 1}.** {_modes.ElementAt(i).Key.ToTitleCase()}";
            }

            selectionBuilder.InputType = InputType.Reactions;
            selectionBuilder.EmoteConverter = x => new Emoji($"{x + 1}\ufe0f\u20e3");
#endif

            var result = await SendSelectionAsync(selectionBuilder.Build(), TimeSpan.FromMinutes(1));

            if (!result.IsSuccess)
            {
                return FergunResult.FromError($"{Locate("ReplyTimeout")} {Locate("CreationCanceled")}", true);
            }

            int modeIndex = result.Value;

            AdventureCreationData creationResponse;
            if (_modes.Keys.ElementAt(modeIndex) == "custom")
            {
                creationResponse = await CreateCustomAdventureAsync(modeIndex, builder, result.Message);
            }
            else
            {
                creationResponse = await CreateAdventureAsync(modeIndex, builder, result.Message);
            }

            if (creationResponse.ErrorMessage != null)
            {
                return FergunResult.FromError(creationResponse.ErrorMessage, creationResponse.IsSilent,
                    creationResponse.IsSilent ? null : creationResponse.Message);
            }

            var actions = creationResponse.Adventure.Actions.Where(x => !string.IsNullOrEmpty(x.Text)).ToList();

            if (actions.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The action list is empty."));
                return FergunResult.FromError(Locate("ErrorInAPI"), false, creationResponse.Message);
            }

            string initialPrompt = actions[^1].Text;
            if (actions.Count > 1)
            {
                actions.RemoveAt(actions.Count - 1);
                initialPrompt = string.Concat(actions.Select(x => x.Text)) + initialPrompt;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(initialPrompt))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Translating text to \"{GetLanguage()}\"."));
                initialPrompt = await TranslateWithFallbackAsync(initialPrompt, "en", GetLanguage());
            }

            builder.Description = initialPrompt.Truncate(EmbedBuilder.MaxDescriptionLength);
            builder.WithFooter($"ID: {creationResponse.Adventure.Id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}", Constants.AiDungeonLogoUrl);

            await creationResponse.Message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            var dbAdventure = new AidAdventure(creationResponse.Adventure.Id, creationResponse.Adventure.PublicId?.ToString(), Context.User.Id, false);
            FergunClient.Database.InsertDocument(Constants.AidAdventuresCollection, dbAdventure);

            return FergunResult.FromSuccess();
        }

        private async Task<AdventureCreationData> CreateAdventureAsync(int modeIndex, EmbedBuilder builder, IUserMessage message)
        {
            var loadingEmbed = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("Loading")}")
                .WithColor(FergunClient.Config.EmbedColor)
                .Build();

            message = await message.ModifyOrResendAsync(embed: loadingEmbed, cache: _messageCache);

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Downloading the character list for mode: {_modes.Keys.ElementAt(modeIndex)} ({_modes.Values.ElementAt(modeIndex)})"));

            AiDungeonScenario scenario;
            try
            {
                scenario = await _api.GetScenarioAsync(_modes.Values.ElementAt(modeIndex));
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception requesting scenario", e));
                return new AdventureCreationData(e.Message, message);
            }

            if (scenario.Options.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The scenario list is empty."));
                return new AdventureCreationData(Locate("ErrorInAPI"), message);
            }

            var characters = scenario
                .Options
                .GroupBy(x => x.Title.Truncate(100), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToDictionary(x => x.Title.Truncate(100), x => x.PublicId.ToString());

            builder.Title = "AI Dungeon";
            builder.Description = Locate("CharacterSelect");

            var warningBuilder = new EmbedBuilder()
                .WithColor(FergunClient.Config.EmbedColor)
                .WithDescription($"\u26a0 {Locate("ReplyTimeout")} {Locate("CreationCanceled")}");

            var selectionBuilder = new SelectionBuilder<int>()
                .AddUser(Context.User)
                .WithInputType(InputType.SelectMenus)
                .WithOptions(Enumerable.Range(0, characters.Count).ToArray())
                .WithStringConverter(x => characters.ElementAt(x).Key.ToTitleCase())
                .WithSelectionPage(PageBuilder.FromEmbedBuilder(builder))
                .WithTimeoutPage(PageBuilder.FromEmbedBuilder(warningBuilder))
                .WithActionOnTimeout(ActionOnStop.ModifyMessage | ActionOnStop.DisableInput);

#if !DNETLABS
            for (int i = 0; i < characters.Count; i++)
            {
                builder.Description += $"\n**{i + 1}.** {characters.ElementAt(i).Key.ToTitleCase()}";
            }

            selectionBuilder.InputType = InputType.Reactions;
            selectionBuilder.EmoteConverter = x => new Emoji($"{x + 1}\ufe0f\u20e3");
            selectionBuilder.ActionOnSuccess = ActionOnStop.DeleteInput;
#endif

            message = await message.Channel.GetMessageAsync(_messageCache, message.Id) as IUserMessage;
            var result = await SendSelectionAsync(selectionBuilder.Build(), TimeSpan.FromMinutes(1), message);

            if (!result.IsSuccess)
            {
                return new AdventureCreationData($"{Locate("ReplyTimeout")} {Locate("CreationCanceled")}", true);
            }

            int characterIndex = result.Value;

            builder.Title = "AI Dungeon";
            builder.Description = FergunClient.Config.LoadingEmote + " " + string.Format(Locate("GeneratingNewAdventure"), _modes.Keys.ElementAt(modeIndex), characters.Keys.ElementAt(characterIndex));

            message = await result.Message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting info for character: {characters.Keys.ElementAt(characterIndex)} ({characters.Values.ElementAt(characterIndex)})"));

            try
            {
                scenario = await _api.GetScenarioAsync(characters.Values.ElementAt(characterIndex));
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception requesting scenario", e));
                return new AdventureCreationData(e.Message, message);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Creating new adventure with character Id: {scenario.Id}"));

            AiDungeonAdventure adventure;
            try
            {
                adventure = await _api.CreateAdventureAsync(scenario.Id.ToString(),
                    scenario.Prompt?.Replace("${character.name}", Context.User.Username, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception creating adventure", e));
                return new AdventureCreationData(e.Message, message);
            }

            string publicId = adventure.PublicId?.ToString();
            if (publicId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: publicId is null."));
                return new AdventureCreationData(Locate("ErrorInAPI"), message);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting adventure with publicId: {publicId}"));

            try
            {
                adventure = await _api.GetAdventureAsync(publicId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception requesting adventure", e));
                return new AdventureCreationData(e.Message, message);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Created adventure ({_modes.Keys.ElementAt(modeIndex)}, {characters.Keys.ElementAt(characterIndex)})" +
                $" (Id: {adventure.Id}, playPublicId: {adventure.PublicId})"));

            return new AdventureCreationData(adventure, message);
        }

        private async Task<AdventureCreationData> CreateCustomAdventureAsync(int modeIndex, EmbedBuilder builder, IUserMessage message)
        {
            builder.Title = Locate("CustomCharacterCreation");
            builder.Description = Locate("CustomCharacterPrompt");

            message = await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            var result = await _interactive.NextMessageAsync(Context.IsSourceUserAndChannel, null, TimeSpan.FromMinutes(5));

            if (!result.IsSuccess)
            {
                return new AdventureCreationData($"{Locate("ReplyTimeout")} {Locate("CreationCanceled")}", message);
            }

            string customText = result.Value!.Content;

            await result.Value.TryDeleteAsync();

            builder.Title = "AI Dungeon";
            builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate("GeneratingNewCustomAdventure")}";

            message = await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            // Get custom adventure ID
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting custom adventure ID... ({_modes.Values.ElementAt(modeIndex)})"));
            AiDungeonScenario scenario;
            try
            {
                scenario = await _api.GetScenarioAsync(_modes.Values.ElementAt(modeIndex));
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception requesting scenario", e));
                return new AdventureCreationData(e.Message, message);
            }

            // Create new adventure with that ID
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Creating custom adventure ({scenario.Id})"));

            AiDungeonAdventure adventure;
            try
            {
                adventure = await _api.CreateAdventureAsync(scenario.Id.ToString());
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception creating adventure", e));
                return new AdventureCreationData(e.Message, message);
            }

            string publicId = adventure.PublicId?.ToString();
            if (publicId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: publicId is null."));
                return new AdventureCreationData(Locate("ErrorInAPI"), message);
            }

            if (AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Translating text from \"{GetLanguage()}\" to English."));
                customText = await TranslateWithFallbackAsync(customText, GetLanguage(), "en");
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Sending action request (publicId: {publicId}, actionType: {ActionType.Story})"));

            try
            {
                adventure = await _api.SendActionAsync(publicId, ActionType.Story, customText);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Exception sending action", e));
                return new AdventureCreationData(e.Message, message);
            }

            return new AdventureCreationData(adventure, message);
        }

        private async Task<string> SendAidCommandAsync(uint adventureId, string promptText, ActionType actionType = ActionType.Continue, string text = "", long actionId = 0)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken));
            }

            var savedAdventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            // check the id
            string checkResult = await CheckIdAsync(savedAdventure);
            if (checkResult != null)
            {
                return checkResult;
            }

            // For the continue command
            if (actionType == ActionType.Continue && !string.IsNullOrEmpty(text))
            {
                // Split the text in two, the first part should be the input type, and the second part, the text.
                string[] splitText = text.Split(' ', 2);
                if (Enum.TryParse(splitText[0], true, out actionType))
                {
                    if (splitText.Length != 1)
                    {
                        text = splitText[1];
                    }
                    if (actionType != ActionType.Do
                        && actionType != ActionType.Say
                        && actionType != ActionType.Story)
                    {
                        actionType = ActionType.Do;
                    }
                }
                else
                {
                    // if the parse fails, keep the text and set the input type to Do (the default).
                    actionType = ActionType.Do;
                }
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate(promptText)}")
                .WithColor(FergunClient.Config.EmbedColor);

            _queue.TryAdd(adventureId, new SemaphoreSlim(1));

            bool wasWaiting = false;
            if (_queue[adventureId].CurrentCount == 0)
            {
                wasWaiting = true;
                builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate("WaitingQueue")}";
            }
            var message = await ReplyAsync(embed: builder.Build());

            AiDungeonAdventure adventure;
            try
            {
                await _queue[adventureId].WaitAsync();

                if (wasWaiting)
                {
                    builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate(promptText)}";
                    await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);
                }

                // if a text is passed
                if (!string.IsNullOrEmpty(text) && AutoTranslate())
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Translating text from \"{GetLanguage()}\" to English."));
                    text = await TranslateWithFallbackAsync(text, GetLanguage(), "en");
                }

                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command",
                    $"AID Action: Sending action request (Id: {savedAdventure.Id}, publicId: {savedAdventure.PublicId}, actionType: {actionType})"));

                adventure = await _api.SendActionAsync(savedAdventure.PublicId, actionType, text, actionId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "AID Action: Exception sending action", e));
                return e.Message;
            }
            finally
            {
                _queue[adventureId].Release();
            }

            var actions = adventure.Actions;
            if (actions.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "AID Action: Action list is empty."));
                return "Action list is empty.";
            }

            string textToShow;

            if (actionType != ActionType.Remember)
            {
                textToShow = actions[^1].Text;
                if (actionType == ActionType.Do ||
                    actionType == ActionType.Say ||
                    actionType == ActionType.Story)
                {
                    textToShow = actions[^2].Text + textToShow;
                }

                if (!string.IsNullOrEmpty(textToShow) && AutoTranslate())
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Translating text to \"{GetLanguage()}\"."));
                    textToShow = await TranslateWithFallbackAsync(textToShow, "en", GetLanguage());
                }
            }
            else
            {
                textToShow = $"{Locate("TheAIWillNowRemember")}\n{text}";
            }

            builder.WithDescription(textToShow.Truncate(EmbedBuilder.MaxDescriptionLength))
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}", Constants.AiDungeonLogoUrl);

            await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            return null;
        }

        [Command("continue", RunMode = RunMode.Async)]
        [Summary("continueSummary")]
        [Alias("next")]
        [Example("2582734 save the princess")]
        public async Task<RuntimeResult> Continue([Summary("continueParam1")] uint adventureId,
            [Remainder, Summary("continueParam2")] string text = "")
        {
            string result = await SendAidCommandAsync(adventureId, "GeneratingStory", text: text);

            return result != null
                ? FergunResult.FromError(result)
                : FergunResult.FromSuccess();
        }

        [Command("undo", RunMode = RunMode.Async)]
        [Summary("undoSummary")]
        [Alias("revert")]
        [Example("2582734")]
        public async Task<RuntimeResult> Undo([Summary("undoParam1")] uint adventureId)
        {
            string result = await SendAidCommandAsync(adventureId, "RevertingLastAction", ActionType.Undo);

            return result != null
                ? FergunResult.FromError(result)
                : FergunResult.FromSuccess();
        }

        [Command("redo", RunMode = RunMode.Async)]
        [Summary("redoSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Redo([Summary("redoParam1")] uint adventureId)
        {
            string result = await SendAidCommandAsync(adventureId, "RedoingLastAction", ActionType.Redo);

            return result != null
                ? FergunResult.FromError(result)
                : FergunResult.FromSuccess();
        }

        [Command("remember", RunMode = RunMode.Async)]
        [Summary("rememberSummary")]
        [Example("2582734 there's a dragon waiting for me")]
        public async Task<RuntimeResult> Remember([Summary("rememberParam1")] uint adventureId,
            [Remainder, Summary("rememberParam2")] string text)
        {
            string result = await SendAidCommandAsync(adventureId, "EditingStoryContext", ActionType.Remember, text);

            return result != null
                ? FergunResult.FromError(result)
                : FergunResult.FromSuccess();
        }

        [Command("alter", RunMode = RunMode.Async)]
        [Summary("alterSummary")]
        [Alias("edit")]
        [Example("2582734")]
        public async Task<RuntimeResult> Alter([Summary("alterParam1")] uint adventureId)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            var savedAdventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            if (savedAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("Loading")}")
                .WithColor(FergunClient.Config.EmbedColor);

            var message = await ReplyAsync(embed: builder.Build());

            AiDungeonAdventure adventure;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Alter: Getting adventure (Id: {savedAdventure.Id},  publicId: {savedAdventure.PublicId})"));
            try
            {
                adventure = await _api.GetAdventureAsync(savedAdventure.PublicId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Alter: Exception requesting adventure", e));
                return FergunResult.FromError(e.Message);
            }

            var actions = adventure.Actions;
            if (actions.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Alter: Action list is empty."));
                return FergunResult.FromError("Action list is empty");
            }

            var lastAction = actions[^1];
            string oldOutput = lastAction.Text;

            if (AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Alter: Translating text to \"{GetLanguage()}\"."));
                oldOutput = await TranslateWithFallbackAsync(oldOutput, "en", GetLanguage());
            }

            builder.WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(string.Format(Locate("NewOutputPrompt"), $"```{oldOutput.Truncate(EmbedBuilder.MaxDescriptionLength - 50)}```"));

            await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            var result = await _interactive.NextMessageAsync(Context.IsSourceUserAndChannel, null, TimeSpan.FromMinutes(5));

            if (!result.IsSuccess)
            {
                return FergunResult.FromError($"{Locate("ReplyTimeout")} {Locate("EditCanceled")}");
            }

            string newOutput = result.Value!.Content.Trim();

            await result.Value.TryDeleteAsync();

            // Wait 1 second before sending or modifying a message to avoid race conditions
            // For some reason the message cache doesn't remove the cached message instantly
            await Task.Delay(1000);

            string commandResult = await SendAidCommandAsync(adventureId, "Loading", ActionType.Alter, newOutput, lastAction.Id);

            return commandResult != null
                ? FergunResult.FromError(commandResult)
                : FergunResult.FromSuccess();
        }

        [Command("retry", RunMode = RunMode.Async)]
        [Summary("retrySummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Retry([Summary("retryParam1")] uint adventureId)
        {
            string result = await SendAidCommandAsync(adventureId, "GeneratingNewResponse", ActionType.Retry);

            return result != null
                ? FergunResult.FromError(result)
                : FergunResult.FromSuccess();
        }

        [Command("makepublic")]
        [Summary("makepublicSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> MakePublic([Summary("makepublicParam1")] uint adventureId)
        {
            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerId)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (adventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPublic"));
            }

            adventure.IsPublic = true;
            FergunClient.Database.InsertOrUpdateDocument(Constants.AidAdventuresCollection, adventure);
            await SendEmbedAsync(Locate("IDNowPublic"));
            return FergunResult.FromSuccess();
        }

        [Command("makeprivate")]
        [Summary("makeprivateSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> MakePrivate([Summary("makeprivateParam1")] uint adventureId)
        {
            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerId)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (!adventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPrivate"));
            }

            adventure.IsPublic = false;
            FergunClient.Database.InsertOrUpdateDocument(Constants.AidAdventuresCollection, adventure);
            await SendEmbedAsync(Locate("IDNowPrivate"));
            return FergunResult.FromSuccess();
        }

        [Command("idlist", RunMode = RunMode.Async)]
        [Alias("ids", "list")]
        [Summary("idlistSummary")]
        [Example("Discord#1234")]
        public async Task<RuntimeResult> IdList([Summary("idlistParam1")] IUser user = null)
        {
            user ??= Context.User;
            var adventures = FergunClient.Database.FindManyDocuments<AidAdventure>(Constants.AidAdventuresCollection, x => x.OwnerId == user.Id).ToArray();

            if (adventures.Length == 0)
            {
                return FergunResult.FromError(string.Format(Locate(user.Id == Context.User.Id ? "SelfNoIDs" : "NoIDs"), user));
            }

            var builder = new EmbedBuilder()
                .WithTitle(string.Format(Locate("IDList"), user))
                .AddField("ID", string.Join("\n", adventures.Select(x => x.Id)), true)
                .AddField(Locate("IsPublic"), string.Join("\n", adventures.Select(x => Locate(x.IsPublic))), true)
                .WithFooter(Locate("IDListFooter"), Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("idinfo", RunMode = RunMode.Async)]
        [Summary("idinfoSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> IdInfo([Summary("idinfoParam1")] uint adventureId)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            var savedAdventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            if (savedAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }

            AiDungeonAdventure adventure;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Idinfo: Getting adventure (Id: {savedAdventure.Id},  publicId: {savedAdventure.PublicId})"));
            try
            {
                adventure = await _api.GetAdventureAsync(savedAdventure.PublicId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Idinfo: Exception requesting adventure", e));
                return FergunResult.FromError(e.Message);
            }

            string initialPrompt;
            var actions = adventure.Actions;

            if (actions.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Idinfo: Action list is empty."));
                initialPrompt = "???";
            }
            else
            {
                initialPrompt = actions[0].Text;
                if (actions.Count > 1)
                {
                    initialPrompt += actions[1].Text;
                }
            }

            var idOwner = Context.IsPrivate ? null : Context.Guild.GetUser(savedAdventure.OwnerId);

            var builder = new EmbedBuilder()
                .WithTitle(Locate("IDInfo"))
                .WithDescription(initialPrompt.Truncate(EmbedBuilder.MaxDescriptionLength))
                .AddField(Locate("IsPublic"), Locate(savedAdventure.IsPublic), true)
                .AddField(Locate("Owner"), idOwner?.ToString() ?? Locate("NotAvailable"), true)
                .WithFooter($"ID: {adventureId} - {Locate("CreatedAt")}:", Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            builder.Timestamp = adventure.CreatedAt;

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("delete", RunMode = RunMode.Async)]
        [Summary("deleteSummary")]
        [Alias("remove")]
        [Example("2582734")]
        public async Task<RuntimeResult> Delete([Summary("deleteParam1")] uint adventureId)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerId)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }

            var builder = new EmbedBuilder()
                .WithDescription(Locate("AdventureDeletionPrompt"))
                .WithColor(FergunClient.Config.EmbedColor);

            var warningBuilder = new EmbedBuilder()
                .WithColor(FergunClient.Config.EmbedColor)
                .WithDescription($"\u26a0 {Locate("ReplyTimeout")}");

            var selection = new EmoteSelectionBuilder()
                .AddOption(new Emoji("🗑"))
                .AddOption(new Emoji("❌"))
                .AddUser(Context.User)
                .WithSelectionPage(PageBuilder.FromEmbedBuilder(builder))
                .WithTimeoutPage(PageBuilder.FromEmbedBuilder(warningBuilder))
                .WithAllowCancel(true)
                .WithActionOnTimeout(ActionOnStop.ModifyMessage | ActionOnStop.DisableInput)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .Build();

            var result = await SendSelectionAsync(selection, TimeSpan.FromSeconds(30));

            if (!result.IsSuccess)
            {
                return FergunResult.FromError(Locate("ReplyTimeout"), true);
            }

            builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("DeletingAdventure")}")
                .WithColor(FergunClient.Config.EmbedColor);

            await result.Message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Delete: Deleting adventure (Id: {adventure.Id},  publicId: {adventure.PublicId})"));

            try
            {
                await _api.DeleteAdventureAsync(adventure.PublicId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Delete: Exception deleting adventure", e));
                return FergunResult.FromError(e.Message, false, result.Message);
            }

            FergunClient.Database.DeleteDocument(Constants.AidAdventuresCollection, adventure);

            await result.Message.ModifyOrResendAsync(embed: builder.WithDescription(Locate("AdventureDeleted")).Build(), cache: _messageCache);

            return FergunResult.FromSuccess();
        }

        [Command("dump", RunMode = RunMode.Async), Ratelimit(1, 2, Measure.Minutes)]
        [Summary("dumpSummary")]
        [Alias("export")]
        [Example("2582734")]
        public async Task<RuntimeResult> Dump([Summary("dumpParam1")] uint adventureId)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            var savedAdventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            string checkResult = await CheckIdAsync(savedAdventure);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("DumpingAdventure")}")
                .WithColor(FergunClient.Config.EmbedColor);

            var message = await ReplyAsync(embed: builder.Build());

            AiDungeonAdventure adventure;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Dump: Getting adventure (Id: {savedAdventure.Id},  publicId: {savedAdventure.PublicId})"));
            try
            {
                adventure = await _api.GetAdventureAsync(savedAdventure.PublicId);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Dump: Exception requesting adventure", e));
                return FergunResult.FromError(e.Message);
            }

            var actions = adventure.Actions;
            if (actions.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Dump: Action list is empty."));
                return FergunResult.FromError("Action list is empty.");
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var hastebinUrl = await Hastebin.UploadAsync(JsonSerializer.Serialize(actions, options));
                builder.Description = Format.Url(Locate("HastebinLink"), hastebinUrl);
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);

            return FergunResult.FromSuccess();
        }

        [Command("give"), Ratelimit(1, 1, Measure.Minutes)]
        [Summary("giveSummary")]
        [Alias("transfer")]
        [Example("2582734")]
        public async Task<RuntimeResult> Give([Summary("giveParam1")] uint adventureId, [Remainder, Summary("giveParam2")] IUser user)
        {
            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerId)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (adventure.PublicId == null)
            {
                return FergunResult.FromError(Locate("PublicIdNull"));
            }
            if (Context.User.Id == user.Id)
            {
                return FergunResult.FromError(Locate("CannotGiveYourself"));
            }
            if (user.IsBot)
            {
                return FergunResult.FromError(Locate("CannotGiveToBot"));
            }

            adventure = new AidAdventure(adventure.Id, adventure.PublicId, user.Id, true);
            FergunClient.Database.InsertOrUpdateDocument(Constants.AidAdventuresCollection, adventure);
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Give: Transferred adventure ID from {Context.User} ({Context.User.Id}) to {user} ({user.Id})."));

            await SendEmbedAsync("✅ " + string.Format(Locate("GaveId"), user));

            return FergunResult.FromSuccess();
        }

        private async Task<string> CheckIdAsync(AidAdventure adventure)
        {
            if (adventure == null)
            {
                return Locate("IDNotFound");
            }
            if (adventure.PublicId == null)
            {
                return Locate("PublicIdNull");
            }
            if (!adventure.IsPublic && Context.User.Id != adventure.OwnerId)
            {
                return string.Format(Locate("IDNotPublic"), await Context.Client.Rest.GetUserAsync(adventure.OwnerId));
            }
            return null;
        }

        private string GetTip()
        {
            var tips = Locate("AIDTips").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            return tips[Random.Shared.Next(tips.Length)];
        }

        private bool AutoTranslate()
        {
            if (GetLanguage() == "en") // AI Dungeon Language
            {
                return false;
            }
            if (Context.IsPrivate)
            {
                return true;
            }
            return GetGuildConfig()?.AidAutoTranslate ?? Constants.AidAutoTranslateDefault;
        }

        // Fallback to original text if fails
        private async Task<string> TranslateWithFallbackAsync(string text, string fromLanguage, string toLanguage)
        {
            try
            {
                var translation = await _translator.TranslateAsync(text, toLanguage, fromLanguage);
                return translation.Result;
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Failed to translate text to \"{toLanguage}\"", e));
                return text;
            }
        }

        private class AdventureCreationData
        {
            public AdventureCreationData(string errorMessage, IUserMessage message)
            {
                ErrorMessage = errorMessage;
                Message = message;
            }

            public AdventureCreationData(string errorMessage, bool isSilent)
            {
                ErrorMessage = errorMessage;
                IsSilent = isSilent;
            }

            public AdventureCreationData(AiDungeonAdventure adventure, IUserMessage message)
            {
                Adventure = adventure;
                Message = message;
            }

            public string ErrorMessage { get; }

            public bool IsSilent { get; }

            public AiDungeonAdventure Adventure { get; }

            public IUserMessage Message { get; }
        }
    }
}