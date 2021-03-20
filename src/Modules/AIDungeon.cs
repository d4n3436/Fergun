using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs;
using Fergun.APIs.AIDungeon;
using Fergun.APIs.BingTranslator;
using Fergun.APIs.GTranslate;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Services;
using Newtonsoft.Json;

namespace Fergun.Modules
{
    using ActionType = APIs.AIDungeon.ActionType;

    [Order(4)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Name("AIDungeon"), Group("aid"), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class AIDungeon : FergunBase
    {
        private static AidAPI _api;
        private static readonly ConcurrentDictionary<uint, SemaphoreSlim> _queue = new ConcurrentDictionary<uint, SemaphoreSlim>();
        private static readonly Random _rng = new Random();
        private static IReadOnlyDictionary<string, string> _modes;

        private static CommandService _cmdService;
        private static LogService _logService;

        public AIDungeon(CommandService commands, LogService logService)
        {
            _api ??= new AidAPI(FergunClient.Config.AiDungeonToken ?? "");
            _cmdService ??= commands;
            _logService ??= logService;
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
                WebSocketResponse response;
                try
                {
                    response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(AidAPI.AllScenariosId, RequestType.GetScenario));
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (WebSocketException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (TimeoutException)
                {
                    return FergunResult.FromError(Locate("ErrorInAPI"));
                }
                string error = CheckResponse(response);
                if (error != null)
                {
                    return FergunResult.FromError(error);
                }
                var content = response.Payload.Data.Scenario;
                if (content?.Options == null || content.Options.Count == 0)
                {
                    if (content?.Options == null)
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: content is null."));
                    }
                    else
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The mode list is empty."));
                    }
                    return FergunResult.FromError(Locate("ErrorInAPI"));
                }
                _modes = content.Options.ToDictionary(x => x.Title, x => x.PublicId?.ToString());
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "New: Using cached mode list..."));
            }

            int modeIndex = -1;
            var stopEvent = new AutoResetEvent(false);
            bool hasReacted = false;
            var builder = new EmbedBuilder();

            async Task HandleAidReactionAsync(int index)
            {
                if (hasReacted) return;
                hasReacted = true;
                modeIndex = index;
                stopEvent.Set();
                await Task.CompletedTask;
            }

            var callbacks = new List<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)>();
            var list = new StringBuilder($"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}\n");
            for (int i = 0; i < _modes.Count; i++)
            {
                int index = i;
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (context, reaction) => await HandleAidReactionAsync(index)));
                list.Append($"**{i + 1}.** {_modes.ElementAt(i).Key.ToTitleCase()}\n");
            }

            builder.WithAuthor(Context.User)
                .WithTitle(Locate("AIDungeonWelcome"))
                .WithDescription(list.ToString())
                .WithThumbnailUrl(Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            var data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(1), async context => await HandleAidReactionAsync(-1));

            data.AddCallbacks(callbacks);
            var message = await InlineReactionReplyAsync(data);
            stopEvent.WaitOne();
            stopEvent.Dispose();

            if (modeIndex == -1)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
            }
            builder.ThumbnailUrl = null;

            string creationError;
            WebSocketAdventure adventure;
            if (_modes.Keys.ElementAt(modeIndex) == "custom")
            {
                await message.TryRemoveAllReactionsAsync();
                (creationError, adventure) = await CreateCustomAdventureAsync(modeIndex, builder);
            }
            else
            {
                await message.DeleteAsync();
                (creationError, adventure) = await CreateAdventureAsync(modeIndex, builder);
            }

            if (creationError != null)
            {
                return FergunResult.FromError(creationError);
            }

            var actionList = adventure.Actions;

            // This should prevent any errors
            if (actionList == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The action list is null."));
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            int removed = actionList.RemoveAll(x => string.IsNullOrEmpty(x.Text));
            if (removed > 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Removed {removed} empty entries in the action list."));
            }

            if (actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The action list is empty."));
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            string initialPrompt = actionList[^1].Text;
            if (actionList.Count > 1)
            {
                actionList.RemoveAt(actionList.Count - 1);
                initialPrompt = string.Concat(actionList.Select(x => x.Text)) + initialPrompt;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(initialPrompt))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Translating text to \"{GetLanguage()}\"."));
                initialPrompt = await TranslateSimplerAsync(initialPrompt, "en", GetLanguage());
            }

            long id = long.Parse(adventure.Id, CultureInfo.InvariantCulture);

            builder.Description = initialPrompt.Truncate(EmbedBuilder.MaxDescriptionLength);
            builder.WithFooter($"ID: {id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}", Constants.AiDungeonLogoUrl);

            await ReplyAsync(embed: builder.Build());

            FergunClient.Database.InsertDocument(Constants.AidAdventuresCollection, new AidAdventure(id, adventure.PublicId?.ToString(), Context.User.Id, false));

            return FergunResult.FromSuccess();
        }

        private async Task<(string, WebSocketAdventure)> CreateAdventureAsync(int modeIndex, EmbedBuilder builder)
        {
            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Downloading the character list for mode: {_modes.Keys.ElementAt(modeIndex)} ({_modes.Values.ElementAt(modeIndex)})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(_modes.Values.ElementAt(modeIndex), RequestType.GetScenario));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }
            string error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }
            var content = response.Payload.Data.Scenario;
            if (content?.Options == null || content.Options.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The scenario list is null or empty."));
                return (Locate("ErrorInAPI"), null);
            }

            int characterIndex = -1;
            bool hasReacted = false;
            var stopEvent = new AutoResetEvent(false);
            var characters = new Dictionary<string, string>(content.Options.ToDictionary(x => x.Title, x => x.PublicId?.ToString()));

            async Task HandleAidReaction2Async(int index)
            {
                if (hasReacted) return;
                hasReacted = true;
                characterIndex = index;
                stopEvent.Set();
                await Task.CompletedTask;
            }

            var callbacks = new List<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)>();
            var list = new StringBuilder();
            for (int i = 0; i < characters.Count; i++)
            {
                int index = i;
                list.Append($"**{i + 1}.** {characters.ElementAt(i).Key.ToTitleCase()}\n");
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (context, reaction) => await HandleAidReaction2Async(index)));
            }

            builder.Title = Locate("CharacterSelect");
            builder.Description = list.ToString();

            var data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(1), async context => await HandleAidReaction2Async(-1));

            data.AddCallbacks(callbacks);

            var message = await InlineReactionReplyAsync(data);
            stopEvent.WaitOne();
            stopEvent.Dispose();

            if (characterIndex == -1)
            {
                return ($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}", null);
            }
            //await message.DeleteAsync();

            builder.Title = "AI Dungeon";
            builder.Description = FergunClient.Config.LoadingEmote + " " + string.Format(Locate("GeneratingNewAdventure"), _modes.Keys.ElementAt(modeIndex), characters.Keys.ElementAt(characterIndex));

            _ = message.TryRemoveAllReactionsAsync();
            await ReplyAsync(embed: builder.Build());

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting info for character: {characters.Keys.ElementAt(characterIndex)} ({characters.Values.ElementAt(characterIndex)})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(characters.Values.ElementAt(characterIndex), RequestType.GetScenario));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            content = response.Payload.Data.Scenario;
            if (content == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The scenario is null."));
                return (Locate("ErrorInAPI"), null);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Creating new adventure with character Id: {content.Id}"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(content.Id, RequestType.CreateAdventure,
                    content.Prompt.Replace("${character.name}", Context.User.Username, StringComparison.OrdinalIgnoreCase)));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            string publicId = response.Payload.Data.AddAdventure?.PublicId?.ToString();
            if (publicId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: publicId is null."));
                return (Locate("ErrorInAPI"), null);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting adventure with publicId: {publicId}"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(publicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            var adventure = response.Payload.Data.Adventure;
            if (adventure == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The adventure is null."));
                return (Locate("ErrorInAPI"), null);
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Created adventure ({_modes.Keys.ElementAt(modeIndex)}, {characters.Keys.ElementAt(characterIndex)})" +
                $" (Id: {adventure.Id}, playPublicId: {adventure.PublicId})"));

            return (null, adventure);
        }

        private async Task<(string, WebSocketAdventure)> CreateCustomAdventureAsync(int modeIndex, EmbedBuilder builder)
        {
            builder.Title = Locate("CustomCharacterCreation");
            builder.Description = Locate("CustomCharacterPrompt");

            var message = await ReplyAsync(embed: builder.Build());
            _ = message.TryRemoveAllReactionsAsync();

            var userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(5));

            if (userInput == null)
            {
                return ($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}", null);
            }

            string customText = userInput.Content;

            await userInput.TryDeleteAsync();

            builder.Title = "AI Dungeon";
            builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate("GeneratingNewCustomAdventure")}";

            await ReplyAsync(embed: builder.Build());

            // Get custom adventure ID
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Getting custom adventure ID... ({_modes.Values.ElementAt(modeIndex)})"));
            WebSocketResponse adventure;
            try
            {
                adventure = await _api.SendWebSocketRequestAsync(new WebSocketRequest(_modes.Values.ElementAt(modeIndex), RequestType.GetScenario));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }

            string error = CheckResponse(adventure);
            if (error != null)
            {
                return (error, null);
            }

            string adventureId = adventure?.Payload?.Data?.Scenario?.Id;
            if (adventureId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The adventure ID is null."));
                return (Locate("ErrorInAPI"), null);
            }

            // Create new adventure with that ID
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Creating custom adventure ({adventureId})"));
            WebSocketResponse creationResponse;
            try
            {
                creationResponse = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventureId, RequestType.CreateAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }

            error = CheckResponse(creationResponse);
            if (error != null)
            {
                return (error, null);
            }

            string publicId = creationResponse.Payload.Data.AddAdventure.PublicId?.ToString();
            if (publicId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: publicId is null."));
                return (Locate("ErrorInAPI"), null);
            }

            if (AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Translating text from \"{GetLanguage()}\" to English."));
                customText = await TranslateSimplerAsync(customText, GetLanguage(), "en");
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: Sending WebSocket request (publicId: {publicId}, actionType: {ActionType.Story})"));
            WebSocketResponse response;
            try
            {
                response = await _api.SendWebSocketRequestAsync(publicId, ActionType.Story, customText);
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                return (e.Message, null);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Websocket exception", e));
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorInAPI"), null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            return (null, response.Payload.Data.Adventure);
        }

        private async Task<string> SendAidCommandAsync(uint adventureId, string promptText, ActionType actionType = ActionType.Continue, string text = "", long actionId = 0)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken));
            }

            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            // check the id
            string checkResult = await CheckIdAsync(adventure);
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

            if (!_queue.ContainsKey(adventureId) && !_queue.TryAdd(adventureId, new SemaphoreSlim(1)))
            {
                return Locate("AnErrorOccurred");
            }

            bool wasWaiting = false;
            if (_queue[adventureId].CurrentCount == 0)
            {
                wasWaiting = true;
                builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate("WaitingQueue")}";
            }
            await ReplyAsync(embed: builder.Build());

            await _queue[adventureId].WaitAsync();

            if (wasWaiting)
            {
                builder.Description = $"{FergunClient.Config.LoadingEmote} {Locate(promptText)}";
                await ReplyAsync(embed: builder.Build());
            }

            // if a text is passed
            if (!string.IsNullOrEmpty(text) && AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Translating text from \"{GetLanguage()}\" to English."));
                text = await TranslateSimplerAsync(text, GetLanguage(), "en");
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Sending WebSocket request (Id: {adventure.Id}, publicId: {adventure.PublicId}, actionType: {actionType})"));
            WebSocketResponse response;
            try
            {
                response = await _api.SendWebSocketRequestAsync(adventure.PublicId, actionType, text, actionId);
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "AID Action: IO exception", e));
                return e.Message;
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "AID Action: Websocket exception", e));
                return e.Message;
            }
            catch (TimeoutException)
            {
                return Locate("ErrorInAPI");
            }
            finally
            {
                _queue[adventureId].Release();
            }

            // check for errors
            string error = CheckResponse(response);
            if (error != null)
            {
                return error;
            }
            var data = response.Payload.Data;

            var actionList = data.SubscribeAdventure?.Actions ?? data.Adventure?.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "AID Action: Action list is null or empty."));
                return Locate("ErrorInAPI");
            }

            string textToShow = actionList[^1].Text;
            if (actionType == ActionType.Do ||
                actionType == ActionType.Say ||
                actionType == ActionType.Story)
            {
                textToShow = actionList[^2].Text + textToShow;
            }

            if (!string.IsNullOrEmpty(textToShow) && AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Translating text to \"{GetLanguage()}\"."));
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            builder.WithDescription(actionType == ActionType.Remember ? $"{Locate("TheAIWillNowRemember")}\n{text}" : textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}", Constants.AiDungeonLogoUrl);

            await ReplyAsync(embed: builder.Build());

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

            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("Loading")}")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Alter: Getting adventure (Id: {adventure.Id},  publicId: {adventure.PublicId})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Alter: IO exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Alter: Websocket exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TimeoutException)
            {
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            string error = CheckResponse(response);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            var actionList = response?.Payload?.Data?.SubscribeAdventure?.Actions ?? response?.Payload?.Data?.Adventure?.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Alter: Action list is null or empty."));
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            var lastAction = actionList[^1];
            string oldOutput = lastAction.Text;

            long actionId = long.Parse(lastAction.Id, CultureInfo.InvariantCulture);

            if (AutoTranslate())
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Alter: Translating text to \"{GetLanguage()}\"."));
                oldOutput = await TranslateSimplerAsync(oldOutput, "en", GetLanguage());
            }

            builder.WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(string.Format(Locate("NewOutputPrompt"), $"```{oldOutput.Truncate(EmbedBuilder.MaxDescriptionLength - 50)}```"));

            await ReplyAsync(embed: builder.Build());

            var userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(5));

            if (userInput == null)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("EditCanceled")}");
            }

            string newOutput = userInput.Content.Trim();

            await userInput.TryDeleteAsync();

            string result = await SendAidCommandAsync(adventureId, "Loading", ActionType.Alter, newOutput, actionId);

            return result != null
                ? FergunResult.FromError(result)
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

            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }

            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Idinfo: Getting adventure (Id: {adventure.Id},  publicId: {adventure.PublicId})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Idinfo: IO exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Idinfo: Websocket exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TimeoutException)
            {
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            string error = CheckResponse(response);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            string initialPrompt;
            var actionList = response?.Payload?.Data?.SubscribeAdventure?.Actions ?? response?.Payload?.Data?.Adventure?.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Idinfo: Action list is null or empty."));
                initialPrompt = "???";
            }
            else
            {
                initialPrompt = actionList[0].Text;
                if (actionList.Count > 1)
                {
                    initialPrompt += actionList[1].Text;
                }
            }

            var idOwner = Context.IsPrivate ? null : Context.Guild.GetUser(adventure.OwnerId);

            var builder = new EmbedBuilder()
                .WithTitle(Locate("IDInfo"))
                .WithDescription(initialPrompt)
                .AddField(Locate("IsPublic"), Locate(adventure.IsPublic), true)
                .AddField(Locate("Owner"), idOwner?.ToString() ?? Locate("NotAvailable"), true)
                .WithFooter($"ID: {adventureId} - {Locate("CreatedAt")}:", Constants.AiDungeonLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            builder.Timestamp = response?.Payload?.Data?.Adventure?.CreatedAt;

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

            bool hasReacted = false;
            IUserMessage message = null;

            var builder = new EmbedBuilder()
                .WithDescription(Locate("AdventureDeletionPrompt"))
                .WithColor(FergunClient.Config.EmbedColor);

            var data = new ReactionCallbackData(null, builder.Build(), true, true, TimeSpan.FromSeconds(30), async context => await HandleReactionAsync(true))
                .AddCallBack(new Emoji("✅"), async (context, reaction) => await HandleReactionAsync(false));

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();

            async Task HandleReactionAsync(bool timeout)
            {
                if (hasReacted) return;
                hasReacted = true;
                if (timeout)
                {
                    await message.ModifyAsync(x => x.Embed = builder.WithDescription($"❌ {Locate("ReactTimeout")}").Build());
                    return;
                }
                string result;
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Delete: Deleting adventure (Id: {adventure.Id},  publicId: {adventure.PublicId})"));
                try
                {
                    var response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.DeleteAdventure));

                    string error = CheckResponse(response);
                    if (error != null)
                    {
                        result = error;
                    }
                    else
                    {
                        FergunClient.Database.DeleteDocument(Constants.AidAdventuresCollection, adventure);
                        result = Locate("AdventureDeleted");
                    }
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Delete: IO exception", e));
                    result = e.Message;
                }
                catch (WebSocketException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Delete: Websocket exception", e));
                    result = e.Message;
                }
                catch (TimeoutException)
                {
                    result = Locate("ErrorInAPI");
                }

                await message.ModifyAsync(x => x.Embed = builder.WithDescription(result).Build());
            }
        }

        [Command("dump", RunMode = RunMode.Async), Ratelimit(1, 20, Measure.Minutes)]
        [Summary("dumpSummary")]
        [Alias("export")]
        [Example("2582734")]
        public async Task<RuntimeResult> Dump([Summary("dumpParam1")] uint adventureId)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.AiDungeonToken))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.AiDungeonToken)));
            }

            var adventure = FergunClient.Database.FindDocument<AidAdventure>(Constants.AidAdventuresCollection, x => x.Id == adventureId);
            string checkResult = await CheckIdAsync(adventure);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.Config.LoadingEmote} {Locate("DumpingAdventure")}")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Dump: Getting adventure (Id: {adventure.Id},  publicId: {adventure.PublicId})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Dump: IO exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (WebSocketException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Dump: Websocket exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TimeoutException)
            {
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            string error = CheckResponse(response);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            var actionList = response.Payload.Data.Adventure.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Dump: Action list is null or empty."));
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            try
            {
                var hastebin = await Hastebin.UploadAsync(string.Join("", actionList.Select(x => x.Text)));
                builder.Description = Format.Url(Locate("HastebinLink"), hastebin.GetLink());
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            await ReplyAsync(embed: builder.Build());

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

        private string CheckResponse(WebSocketResponse response)
        {
            if (response == null)
            {
                return Locate("ErrorInAPI");
            }
            if (response.Payload?.Message != null)
            {
                return response.Payload.Message;
            }
            if (response.Payload?.Errors != null && response.Payload.Errors.Count > 0)
            {
                return response.Payload.Errors[0].Message;
            }
            var data = response.Payload?.Data;
            if (data == null)
            {
                return Locate("ErrorInAPI");
            }
            if (data.Errors != null && data.Errors.Count > 0)
            {
                return data.Errors[0].Message;
            }
            if (data.AddAction != null)
            {
                return data.AddAction.Message;
            }
            if (data.EditAction?.Message != null)
            {
                return data.EditAction.Message;
            }

            return data.SubscribeAdventure?.Error != null
                ? data.SubscribeAdventure.Error.Message
                : data.Adventure?.Error?.Message;
        }

        private string GetTip()
        {
            var tips = Locate("AIDTips").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            return tips[_rng.Next(tips.Length)];
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
        private static async Task<string> TranslateSimplerAsync(string text, string from, string to)
        {
            try
            {
                using var translator = new GTranslator();
                var result = await translator.TranslateAsync(text, to, from);
                return result.Translation;
            }
            catch (Exception e) when (e is TranslationException || e is JsonSerializationException || e is HttpRequestException || e is ArgumentException)
            {
                try
                {
                    var result = await BingTranslatorApi.TranslateAsync(text, to, from);
                    return result[0].Translations[0].Text;
                }
                catch (Exception e2) when (e2 is JsonSerializationException || e2 is HttpRequestException || e2 is TaskCanceledException)
                {
                    return text;
                }
            }
        }
    }
}