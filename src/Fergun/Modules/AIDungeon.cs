using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs;
using Fergun.APIs.AIDungeon;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Services;
using GoogleTranslateFreeApi;

namespace Fergun.Modules
{
    using ActionType = APIs.AIDungeon.ActionType;

    [RequireBotPermission(Constants.MinimunRequiredPermissions)]
    [Group("aid"), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class AIDungeon : FergunBase
    {
        public const string IconUrl = "https://fergun.is-inside.me/CypOix5S.png";

        private static APIs.AIDungeon.AIDungeon _api;
        private static readonly ConcurrentDictionary<uint, SemaphoreSlim> _queue = new ConcurrentDictionary<uint, SemaphoreSlim>();
        private static readonly Random _rng = new Random();
        private static IReadOnlyDictionary<string, string> _modes;

        private static CommandService _cmdService;
        private static LogService _logService;

        public AIDungeon(CommandService commands, LogService logService)
        {
            _api ??= new APIs.AIDungeon.AIDungeon(FergunConfig.AiDungeonToken);
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

            var aidCommands = _cmdService.Modules.FirstOrDefault(x => x.Group == "aid");
            if (aidCommands == null)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            string list = "";
            foreach (var command in aidCommands.Commands)
            {

                list += $"`{command.Name}";
                foreach (var parameter in command.Parameters)
                {
                    list += ' ';
                    list += parameter.IsOptional ? '[' : '<';
                    list += parameter.Name;
                    if (parameter.IsRemainder || parameter.IsMultiple)
                        list += "...";
                    list += parameter.IsOptional ? ']' : '>';
                }
                list += $"`: {Locate(command.Summary)}\n\n";
            }
            
            builder.AddField(Locate("Commands"), list)
                .WithFooter(Locate("HelpFooter2"), IconUrl)
                //.AddField("Tips", "- " + string.Join("\n- ", GetValue("AIDTips").Split(new string[] { Environment.NewLine }, StringSplitOptions.None)))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("new", RunMode = RunMode.Async), Ratelimit(1, 1, Measure.Minutes)]
        [Summary("aidnewSummary")]
        public async Task<RuntimeResult> New()
        {
            if (_modes == null)
            {
                await Context.Channel.TriggerTypingAsync();
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "New: Downloading the mode list..."));
                WebSocketResponse response;
                try
                {
                    response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(APIs.AIDungeon.AIDungeon.AllScenariosId, RequestType.GetScenario));
                }
                catch (IOException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: IO exception", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (TimeoutException)
                {
                    return FergunResult.FromError(Locate("ErrorOnAPI"));
                }
                catch (Exception e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Unknown error", e));
                    return FergunResult.FromError(e.Message);
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
                    return FergunResult.FromError(Locate("ErrorOnAPI"));
                }
                _modes = content.Options.ToDictionary(x => x.Title, x => x.PublicId?.ToString());
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "New: Using cached mode list..."));
            }

            int modeIndex = -1;
            AutoResetEvent stopEvent = new AutoResetEvent(false);
            bool hasReacted = false;
            IUserMessage message = null;
            var builder = new EmbedBuilder();

            async Task HandleAidReactionAsync(int index)
            {
                modeIndex = index;
                stopEvent.Set();
                await Task.CompletedTask;
            }

            var callbacks = new List<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)>();
            string list = $"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}\n";
            for (int i = 0; i < _modes.Count; i++)
            {
                int index = i;
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (_, _1) =>
                {
                    hasReacted = true;
                    await HandleAidReactionAsync(index);
                }
                ));
                list += $"**{i + 1}.** {_modes.ElementAt(i).Key.ToTitleCase()}\n";
            }

            builder.WithAuthor(Context.User)
                .WithTitle(Locate("AIDungeonWelcome"))
                .WithDescription(list)
                .WithThumbnailUrl(IconUrl)
                .WithColor(FergunConfig.EmbedColor);

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(1),
                async (_) =>
                {
                    if (!hasReacted)
                    {
                        stopEvent.Set();
                        await Task.CompletedTask;
                    }
                });

            data.AddCallbacks(callbacks);
            message = await InlineReactionReplyAsync(data);
            stopEvent.WaitOne();
            stopEvent.Dispose();

            if (modeIndex == -1)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
            }
            builder.ThumbnailUrl = null;

            string creationError;
            WebSocketAdventure adventure;
            if (_modes.Keys.ElementAt(modeIndex) == "custom" || _modes.Keys.ElementAt(modeIndex) == "archive")
            {
                await message.TryRemoveAllReactionsAsync();
                return FergunResult.FromError("Custom/Archive adventure modes are currently unsupported. Please select other mode.");
                //(creationError, adventure) = await CreateCustomAdventureAsync(modeIndex, builder);
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
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            int removed = actionList.RemoveAll(x => string.IsNullOrEmpty(x.Text));
            if (removed > 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"New: Removed {removed} empty entries in the action list."));
            }

            if (actionList.Count == 0)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "New: The action list is empty."));
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }


            string initialPrompt = actionList[actionList.Count - 1].Text;
            if (actionList.Count > 1)
            {
                string previousAction = "";
                actionList.RemoveAt(actionList.Count - 1);
                foreach (var action in actionList)
                {
                    previousAction += action.Text;
                }
                initialPrompt = previousAction + initialPrompt;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(initialPrompt))
            {
                initialPrompt = await TranslateSimplerAsync(initialPrompt, "en", GetLanguage());
            }

            uint id = uint.Parse(adventure.Id, CultureInfo.InvariantCulture);

            builder.Description = initialPrompt.Truncate(EmbedBuilder.MaxDescriptionLength);
            builder.WithFooter($"ID: {id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}", IconUrl);

            await ReplyAsync(embed: builder.Build());

            FergunClient.Database.InsertRecord("AIDAdventures", new AidAdventure(id, adventure.PublicId?.ToString(), Context.User.Id, false));

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
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Unknown exception", e));
                return (e.Message, null);
            }
            string error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }
            var content = response.Payload.Data.Scenario;
            if (content?.Options == null || content.Options.Count == 0)
            {
                return (Locate("ErrorOnAPI"), null);
            }

            int characterIndex = -1;
            bool hasReacted = false;
            AutoResetEvent stopEvent = new AutoResetEvent(false);

            string list = "";
            var characters =
                new Dictionary<string, string>(content.Options.ToDictionary(x => x.Title, x => x.PublicId?.ToString()));

            async Task HandleAidReaction2Async(int index)
            {
                characterIndex = index;
                stopEvent.Set();
                await Task.CompletedTask;
            }

            var callbacks = new List<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)>();
            for (int i = 0; i < characters.Count; i++)
            {
                int index = i;
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (_, _1) =>
                {
                    hasReacted = true;
                    await HandleAidReaction2Async(index);
                }
                ));

                // Idk why adding this way can lead to unordered or repeated values
                //characters.Add(options[i].Title, uint.Parse(options[i].Id.Substring(9))); // "scenario:xxxxxx"
                list += $"**{i + 1}.** {characters.ElementAt(i).Key.ToTitleCase()}\n";
            }

            builder.Title = Locate("CharacterSelect");
            builder.Description = list;

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(1),
                async (_) =>
                {
                    if (!hasReacted)
                    {
                        stopEvent.Set();
                    }
                    await Task.CompletedTask;
                });

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
            builder.Description = string.Format(Locate("GeneratingNewAdventure"), _modes.Keys.ElementAt(modeIndex), characters.Keys.ElementAt(characterIndex));

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
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Unknown exception", e));
                return (e.Message, null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            content = response.Payload.Data.Scenario;
            if (content == null)
            {
                return (Locate("ErrorOnAPI"), null);
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
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Unknown exception", e));
                return (e.Message, null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            string publicId = response.Payload.Data.CreateAdventure?.PublicId?.ToString();
            if (publicId == null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"New: publicId is null."));
                return (Locate("ErrorOnAPI"), null);
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
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            catch (Exception e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "New: Unknown exception", e));
                return (e.Message, null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }


            var adventure = response.Payload.Data.Adventure;
            if (adventure == null)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command",$"New: Created adventure ({_modes.Keys.ElementAt(modeIndex)}, {characters.Keys.ElementAt(characterIndex)})" +
                $" (Id: {adventure.Id}, playPublicId: {adventure.PublicId})"));

            return (null, adventure);

        }

        private async Task<(string, WebSocketAdventure)> CreateCustomAdventureAsync(int modeIndex, EmbedBuilder builder)
        {
            builder.Title = Locate("CustomChararacterCreation");
            builder.Description = Locate("CustomCharacterPrompt");

            var message = await ReplyAsync(embed: builder.Build());
            _ = message.TryRemoveAllReactionsAsync();

            var userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(5));

            if (userInput == null)
            {
                return ($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}", null);
            }

            string customText = userInput.Content;

            if (customText.Length > 140)
            {
                return ($"{Locate("140CharsMax")} {Locate("CreationCanceled")}", null);
            }

            await userInput.TryDeleteAsync();

            builder.Title = "AI Dungeon";
            builder.Description = Locate("GeneratingNewCustomAdventure");

            await ReplyAsync(embed: builder.Build());

            CreationResponse adventure = null;
            try
            {
                //adventure = await _api.CreateAdventureAsync(_modes.Values.ElementAt(modeIndex));
            }
            catch (HttpRequestException e)
            {
                return (e.Message, null);
            }
            string error = CheckResponse(adventure);
            if (error != null)
            {
                return (error, null);
            }

            if (AutoTranslate())
            {
                customText = await TranslateSimplerAsync(customText, GetLanguage(), "en");
            }

            //uint id = uint.Parse(adventure.Data.AdventureInfo.ContentId, CultureInfo.InvariantCulture);
            string id = null;

            WebSocketResponse response;
            try
            {
                response = await _api.SendWebSocketRequestAsync(id, ActionType.Story, customText);
            }
            catch (IOException e)
            {
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            error = CheckResponse(response);
            if (error != null)
            {
                return (error, null);
            }

            return (null, response.Payload.Data.SubscribeAdventure);
        }

        private async Task<string> RunAIDCommandAsync(uint adventureId, string promptText, ActionType actionType = ActionType.Continue, string text = "", uint actionId = 0)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);
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
                .WithDescription($"{Constants.LoadingEmote} {Locate(promptText)}")
                .WithColor(FergunConfig.EmbedColor);

            if (!_queue.ContainsKey(adventureId))
            {
                if (!_queue.TryAdd(adventureId, new SemaphoreSlim(1)))
                {
                    return Locate("AnErrorOccurred");
                }
            }

            bool wasWaiting = false;
            if (_queue[adventureId].CurrentCount == 0)
            {
                wasWaiting = true;
                builder.Description = $"{Constants.LoadingEmote} {Locate("WaitingQueue")}";
            }
            await ReplyAsync(embed: builder.Build());

            await _queue[adventureId].WaitAsync();

            if (wasWaiting)
            {
                builder.Description = $"{Constants.LoadingEmote} {Locate(promptText)}";
                await ReplyAsync(embed: builder.Build());
            }

            // if a text is passed
            if (!string.IsNullOrEmpty(text) && AutoTranslate())
            {
                text = await TranslateSimplerAsync(text, GetLanguage(), "en");
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"AID Action: Sending WebSocket request (Id: {adventure.ID}, publicId: {adventure.PublicId}, actionType: {actionType})"));
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
            catch (TimeoutException)
            {
                return Locate("ErrorOnAPI");
            }
            catch (Exception)
            {
                // Catch and throw any other exception to prevent locks
                throw;
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

            var actionList = data.SubscribeAdventure?.Actions ?? data.PlayerAction?.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                return Locate("ErrorOnAPI");
            }

            string textToShow = actionList[actionList.Count - 1].Text;
            if (actionType == ActionType.Do ||
                actionType == ActionType.Say ||
                actionType == ActionType.Story)
            {
                textToShow = actionList[actionList.Count - 2].Text + textToShow;
            }

            if (!string.IsNullOrEmpty(textToShow) && AutoTranslate())
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            builder.WithDescription((actionType == ActionType.Remember ? $"{Locate("TheAIWillNowRemember")}\n" : "") + textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}", IconUrl);

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
            string result = await RunAIDCommandAsync(adventureId, "GeneratingStory", text: text);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("undo", RunMode = RunMode.Async)]
        [Summary("undoSummary")]
        [Alias("revert")]
        [Example("2582734")]
        public async Task<RuntimeResult> Undo([Summary("undoParam1")] uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "RevertingLastAction", ActionType.Undo);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("redo", RunMode = RunMode.Async)]
        [Summary("redoSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Redo([Summary("redoParam1")] uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "RedoingLastAction", ActionType.Redo);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("remember", RunMode = RunMode.Async)]
        [Summary("rememberSummary")]
        [Example("2582734 there's a dragon waiting for me")]
        public async Task<RuntimeResult> Remember([Summary("rememberParam1")] uint adventureId,
            [Remainder, Summary("rememberParam2")] string text)
        {
            string result = await RunAIDCommandAsync(adventureId, "EditingStoryContext", ActionType.Remember, text);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("alter", RunMode = RunMode.Async)]
        [Summary("alterSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Alter(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{Constants.LoadingEmote} {Locate("Loading")}")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            AdventureInfoResponse adventure;
            try
            {
                adventure = await _api.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            string error = CheckResponse(adventure);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            var historyList = adventure.Data.Content.HistoryList;

            if (historyList.Count == 0)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            string oldOutput = historyList[historyList.Count - 1].Text;
            uint actionId = uint.Parse(historyList[historyList.Count - 1].Id, CultureInfo.InvariantCulture);

            if (AutoTranslate())
            {
                oldOutput = await TranslateSimplerAsync(oldOutput, "en", GetLanguage());
            }

            builder.WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(string.Format(Locate("NewOutputPrompt"), $"```{oldOutput}```"));

            await ReplyAsync(embed: builder.Build());

            var userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(5));

            if (userInput == null)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("EditCanceled")}");
            }

            string newOutput = userInput.Content.Trim();

            if (newOutput.Length > 140)
            {
                return FergunResult.FromError($"{Locate("140CharsMax")} {Locate("EditCanceled")}");
            }

            await userInput.TryDeleteAsync();

            string result = await RunAIDCommandAsync(adventureId, "Loading", ActionType.Alter, newOutput, actionId);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("retry", RunMode = RunMode.Async)]
        [Summary("retrySummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Retry([Summary("retryParam1")] uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "GeneratingNewResponse", ActionType.Retry);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("makepublic")]
        [Summary("makepublicSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Makepublic([Summary("makepublicParam1")] uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerID)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (adventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPublic"));
            }

            adventure.IsPublic = true;
            FergunClient.Database.UpdateRecord("AIDAdventures", adventure);
            await SendEmbedAsync(Locate("IDNowPublic"));
            return FergunResult.FromSuccess();
        }

        [Command("makeprivate")]
        [Summary("makeprivateSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Makeprivate([Summary("makeprivateParam1")] uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerID)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (!adventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPrivate"));
            }

            adventure.IsPublic = false;
            FergunClient.Database.UpdateRecord("AIDAdventures", adventure);
            await SendEmbedAsync(Locate("IDNowPrivate"));
            return FergunResult.FromSuccess();
        }

        [Command("idlist", RunMode = RunMode.Async)]
        [Alias("ids", "list")]
        [Summary("idlistSummary")]
        [Example("Discord#1234")]
        public async Task<RuntimeResult> Idlist([Summary("idlistParam1")] IUser user = null)
        {
            user ??= Context.User;
            List<AidAdventure> adventures = FergunClient.Database.FindMany<AidAdventure>("AIDAdventures", x => x.OwnerID == user.Id);

            if (adventures.Count == 0)
            {
                return FergunResult.FromError(string.Format(Locate(user.Id == Context.User.Id ? "SelfNoIDs" : "NoIDs"), user.ToString()));
            }

            var builder = new EmbedBuilder()
                .WithTitle(string.Format(Locate("IDList"), user.ToString()))
                .AddField("ID", string.Join("\n", adventures.Select(x => x.ID)), true)
                .AddField(Locate("IsPublic"), string.Join("\n", adventures.Select(x => Locate(x.IsPublic))), true)
                .WithFooter(Locate("IDListFooter"), IconUrl)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("idinfo", RunMode = RunMode.Async)]
        [Summary("idinfoSummary")]
        [Example("2582734")]
        public async Task<RuntimeResult> Idinfo([Summary("idinfoParam1")] uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);
            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }

            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Idinfo: Getting adventure (Id: {adventure.ID},  publicId: {adventure.PublicId})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Idinfo: IO exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TimeoutException)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            string error = CheckResponse(response);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            string initialPrompt;
            var actionList = response?.Payload?.Data?.SubscribeAdventure?.Actions ?? response?.Payload?.Data?.PlayerAction?.Actions;
            if (actionList == null || actionList.Count == 0)
            {
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

            var idOwner = Context.IsPrivate ? null : Context.Guild.GetUser(adventure.OwnerID);

            var builder = new EmbedBuilder()
                .WithTitle(Locate("IDInfo"))
                .WithDescription(initialPrompt)
                .AddField(Locate("IsPublic"), Locate(adventure.IsPublic), true)
                .AddField(Locate("Owner"), idOwner?.ToString() ?? Locate("NotAvailable"), true)
                .WithFooter($"ID: {adventureId} - {Locate("CreatedAt")}:", IconUrl)
                .WithColor(FergunConfig.EmbedColor);

            if (response.Payload.Data.Adventure.CreatedAt != null)
            {
                builder.Timestamp = response.Payload.Data.Adventure.CreatedAt;
            }

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("delete", RunMode = RunMode.Async)]
        [Summary("deleteSummary")]
        [Alias("remove")]
        [Example("2582734")]
        public async Task<RuntimeResult> Delete([Summary("deleteParam1")] uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (adventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != adventure.OwnerID)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }

            bool hasReacted = false;
            IUserMessage message = null;

            var builder = new EmbedBuilder()
                .WithDescription(Locate("AdventureDeletionPrompt"))
                .WithColor(FergunConfig.EmbedColor);

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), true, true, TimeSpan.FromSeconds(30),
                async (_) =>
                {
                    if (!hasReacted)
                    {
                        await message.ModifyAsync(x => x.Embed = builder.WithDescription($"❌ {Locate("ReactTimeout")}").Build());
                    }
                })
                .AddCallBack(new Emoji("✅"), async (_, _1) =>
                {
                    hasReacted = true;
                    string result;
                    WebSocketResponse response;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Delete: Deleting adventure (Id: {adventure.ID},  publicId: {adventure.PublicId})"));
                    try
                    {
                        response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.DeleteAdventure));

                        string error = CheckResponse(response);
                        if (error != null)
                        {
                            result = error;
                        }
                        else
                        {
                            FergunClient.Database.DeleteRecord("AIDAdventures", adventure);
                            result = Locate("AdventureDeleted");
                        }
                    }
                    catch (IOException e)
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Delete: IO exception", e));
                        result = e.Message;
                    }
                    catch (TimeoutException)
                    {
                        result = Locate("ErrorOnAPI");
                    }
                    
                    await message.ModifyAsync(x => x.Embed = builder.WithDescription(result).Build());
                });

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();
        }

        [Command("dump", RunMode = RunMode.Async), Ratelimit(1, 20, Measure.Minutes)]
        [Summary("dumpSummary")]
        [Alias("export")]
        [Example("2582734")]
        public async Task<RuntimeResult> Dump([Summary("dumpParam1")] uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);
            string checkResult = await CheckIdAsync(adventure);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{Constants.LoadingEmote} {Locate("DumpingAdventure")}")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            WebSocketResponse response;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Dump: Getting adventure (Id: {adventure.ID},  publicId: {adventure.PublicId})"));
            try
            {
                response = await _api.SendWebSocketRequestAsync(new WebSocketRequest(adventure.PublicId, RequestType.GetAdventure));
            }
            catch (IOException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Error, "Command", "Dump: IO exception", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TimeoutException)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            string error = CheckResponse(response);
            if (error != null)
            {
                return FergunResult.FromError(error);
            }

            var actionList = response.Payload.Data.Adventure.Actions;
            if (actionList == null || actionList.Count == 0)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            try
            {
                var hastebin = await Hastebin.UploadAsync(string.Join("", actionList.Select(x => x.Text)));
                builder.Description = Format.Url(Locate("HastebinLink"), hastebin.GetLink());
            }
            catch (HttpRequestException)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            await ReplyAsync(embed: builder.Build());

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
            if (!adventure.IsPublic && Context.User.Id != adventure.OwnerID)
            {
                return string.Format(Locate("IDNotPublic"), (await Context.Client.Rest.GetUserAsync(adventure.OwnerID)).ToString());
            }
            return null;
        }

        private Task<string> CheckIdAsync(uint adventureId)
        {
            AidAdventure adventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);
            return CheckIdAsync(adventure);
        }

        private string CheckResponse(WebSocketResponse response)
        {
            if (response?.Payload?.Message != null)
            {
                return response.Payload.Message;
            }
            var data = response?.Payload?.Data;
            if (data == null)
            {
                return Locate("ErrorOnAPI");
            }
            if (data.Errors != null && data.Errors.Count > 1)
            {
                return data.Errors[0].Message;
            }
            if (data.SubscribeAdventure?.Error != null)
            {
                return data.SubscribeAdventure.Error.Message;
            }
            if (data.Adventure?.Error != null)
            {
                return data.Adventure.Error.Message;
            }
            return null;
        }

        private string CheckResponse(IResponse response)
        {
            // Invalid response content
            if (response == null)
            {
                return Locate("ErrorOnAPI");
            }
            if (response.Errors != null)
            {
                return response.Errors[0].Message;
            }
            return null;
        }

        private string GetTip()
        {
            var tips = Locate("AIDTips").Split(Environment.NewLine, StringSplitOptions.None);
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
            return GetGuildConfig()?.AidAutoTranslate ?? FergunConfig.AidAutoTranslateDefault;
        }

        // Fallback to original text if fails
        private static async Task<string> TranslateSimplerAsync(string text, string from, string to)
        {
            try
            {
                var translator = new GoogleTranslator();
                var result = await translator.TranslateLiteAsync(text, new Language("", from), new Language("", to));
                return result.MergedTranslation;
            }
            catch
            {
                try
                {
                    var result = await Translators.TranslateBingAsync(text, to, from);
                    return result[0].Translations[0].Text;
                }
                catch
                {
                    return text;
                }
            }
        }
    }
}