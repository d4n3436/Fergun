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
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using GoogleTranslateFreeApi;

namespace Fergun.Modules
{
    using ActionType = APIs.AIDungeon.ActionType;

    [Group("aid"), Ratelimit(2, FergunClient.GlobalCooldown, Measure.Minutes)]
    public class AIDungeon : FergunBase
    {
        public const string IconUrl = "https://fergun.is-inside.me/CypOix5S.png";
        private static APIs.AIDungeon.AIDungeon API;
        private static readonly ConcurrentDictionary<uint, SemaphoreSlim> _queue = new ConcurrentDictionary<uint, SemaphoreSlim>();
        private static readonly Random _rng = new Random();
        private static SortedList<string, uint> _modes;

        public AIDungeon()
        {
            API ??= new APIs.AIDungeon.AIDungeon(FergunConfig.AiDungeonToken);
        }

        [Command("info")]
        public async Task Info()
        {
            var builder = new EmbedBuilder()
                .WithTitle(Locate("AIDHelp"))
                .AddField(Locate("AboutAIDTitle"), Locate("AboutAIDText"))
                .AddField(Locate("AIDHowToPlayTitle"), Locate("AIDHowToPlayText"))
                .AddField(Locate("InputTypes"), Locate("InputTypesList"))
                .AddField(Locate("Commands"), Locate("AIDCommandList"))
                .WithFooter(Locate("HelpFooter2"), IconUrl)
                //.AddField("Tips", "- " + string.Join("\n- ", GetValue("AIDTips").Split(new string[] { Environment.NewLine }, StringSplitOptions.None)))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("new", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> New()
        {
            string list = $"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}\n";
            string responseError;
            if (_modes == null)
            {
                await Context.Channel.TriggerTypingAsync();
                ScenarioResponse response;
                try
                {
                    response = await API.GetScenarioAsync(APIs.AIDungeon.AIDungeon.AllScenarios);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(response);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }
                _modes = new SortedList<string, uint>(response.Data.Content.Options.ToDictionary(x => x.Title, x => uint.Parse(x.Id.Substring(9))));
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
            for (int i = 0; i < _modes.Count; i++)
            {
                int index = i;
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (_, _1) =>
                {
                    hasReacted = true;
                    await HandleAidReactionAsync(index);
                }));
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

            (string, List<History>) result;
            if (_modes.Keys[modeIndex] == "custom")
            {
                result = await CreateCustomAdventureAsync(modeIndex, builder);
            }
            else
            {
                await message.DeleteAsync();
                result = await CreateAdventureAsync(modeIndex, builder);
            }

            if (result.Item1 != null)
            {
                return FergunResult.FromError(result.Item1);
            }

            var historyList = result.Item2;
            historyList.RemoveAll(x => string.IsNullOrEmpty(x.Text));

            // This should prevent any errors
            if (historyList.Count == 0)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            string initialPrompt = historyList[historyList.Count - 1].Text;
            if (historyList.Count > 1)
            {
                string previousHistory = "";
                historyList.RemoveAt(historyList.Count - 1);
                foreach (var history in historyList)
                {
                    previousHistory += history.Text;
                }
                initialPrompt = previousHistory + initialPrompt;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(initialPrompt))
            {
                initialPrompt = await TranslateSimplerAsync(initialPrompt, "en", GetLanguage());
            }

            uint id = uint.Parse(result.Item2[0].AdventureId, CultureInfo.InvariantCulture);

            builder.Description = initialPrompt;
            builder.WithFooter($"ID: {id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}", IconUrl);

            await ReplyAsync(embed: builder.Build());

            FergunClient.Database.InsertRecord("AIDAdventures", new AidAdventure(id, Context.User.Id, false));

            return FergunResult.FromSuccess();
        }

        private async Task<(string, List<History>)> CreateAdventureAsync(int modeIndex, EmbedBuilder builder)
        {
            ScenarioResponse scenarioResponse;
            try
            {
                scenarioResponse = await API.GetScenarioAsync(_modes.Values[modeIndex]);
            }
            catch (HttpRequestException e)
            {
                return (e.Message, null);
            }
            string responseError = CheckResponse(scenarioResponse);
            if (responseError != null)
            {
                return (responseError, null);
            }

            int characterIndex = -1;
            bool hasReacted = false;
            AutoResetEvent stopEvent = new AutoResetEvent(false);

            string list = "";
            SortedList<string, uint> characters =
                new SortedList<string, uint>(scenarioResponse.Data.Content.Options.ToDictionary(x => x.Title, x => uint.Parse(x.Id.Substring(9))));

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
            builder.Description = string.Format(Locate("GeneratingNewAdventure"), _modes.Keys[modeIndex], characters.Keys[characterIndex]);

            await message.RemoveAllReactionsAsync();
            await ReplyAsync(embed: builder.Build());

            ScenarioResponse scenario;
            try
            {
                scenario = await API.GetScenarioAsync(characters.Values[characterIndex]);
            }
            catch (HttpRequestException e)
            {
                return (e.Message, null);
            }
            responseError = CheckResponse(scenario);
            if (responseError != null)
            {
                return (responseError, null);
            }

            CreationResponse response;
            try
            {
                response = await API.CreateAdventureAsync(characters.Values[characterIndex], scenario.Data.Content.Prompt.Replace("${character.name}", Context.User.Username, StringComparison.OrdinalIgnoreCase));
            }
            catch (HttpRequestException e)
            {
                return (e.Message, null);
            }
            responseError = CheckResponse(response);
            if (responseError != null)
            {
                return (responseError, null);
            }

            return (null, response.Data.AdventureInfo.HistoryList);

        }

        private async Task<(string, List<History>)> CreateCustomAdventureAsync(int modeIndex, EmbedBuilder builder)
        {
            builder.Title = Locate("CustomChararacterCreation");
            builder.Description = Locate("CustomCharacterPrompt");

            var message = await ReplyAsync(embed: builder.Build());
            await message.RemoveAllReactionsAsync();

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

            CreationResponse adventure;
            try
            {
                adventure = await API.CreateAdventureAsync(_modes.Values[modeIndex]);
            }
            catch (HttpRequestException e)
            {
                return (e.Message, null);
            }
            string responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                return (responseError, null);
            }

            if (AutoTranslate())
            {
                customText = await TranslateSimplerAsync(customText, GetLanguage(), "en");
            }

            uint id = uint.Parse(adventure.Data.AdventureInfo.ContentId, CultureInfo.InvariantCulture);

            WebSocketActionResponse response;
            try
            {
                response = await API.RunWebSocketActionAsync(id, ActionType.Story, customText);
            }
            catch (IOException e)
            {
                return (e.Message, null);
            }
            catch (TimeoutException)
            {
                return (Locate("ErrorOnAPI"), null);
            }
            responseError = CheckResponse(response);
            if (responseError != null)
            {
                return (responseError, null);
            }

            return (null, response.Payload.Data.SubscribeContent.HistoryList);
        }

        /*
        [Command("new", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> New()
        {
            string list = $"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}\n";
            string responseError;
            if (_modes == null)
            {
                await Context.Channel.TriggerTypingAsync();
                ScenarioResponse response;
                try
                {
                    response = await API.GetScenarioAsync(APIs.AIDungeon.AIDungeon.AllScenarios);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(response);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }
                _modes = new SortedList<string, uint>(response.Data.Content.Options.ToDictionary(x => x.Title, x => uint.Parse(x.Id.Substring(9))));
            }

            for (int i = 0; i < _modes.Count; i++)
            {
                list += $"**{i + 1}.** {_modes.ElementAt(i).Key.ToTitleCase()}\n";
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(Locate("AIDungeonWelcome"))
                .WithDescription(list)
                .WithThumbnailUrl(IconUrl)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            builder.ThumbnailUrl = null;

            var userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(1));

            if (userInput == null)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
            }

            if (int.TryParse(userInput.Content, out int modeOption))
            {
                if (modeOption < 1 || modeOption > _modes.Count)
                {
                    return FergunResult.FromError($"{Locate("OutOfIndex")} {Locate("CreationCanceled")}");
                }
                modeOption--;
            }
            else
            {
                if (userInput.Content.ToLowerInvariant() == $"{GetPrefix()}aid info")
                {
                    return FergunResult.FromSuccess();
                }
                modeOption = _modes.IndexOfKey(userInput.Content.ToLowerInvariant());
                if (modeOption == -1)
                {
                    return FergunResult.FromError($"{Locate("InvalidOption")} {Locate("CreationCanceled")}");
                }
            }

            List<History> historyList;
            uint id;

            if (_modes.Keys[modeOption] != "custom")
            {
                ScenarioResponse scenarioResponse;
                try
                {
                    scenarioResponse = await API.GetScenarioAsync(_modes.Values[modeOption]);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(scenarioResponse);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                list = "";
                SortedList<string, uint> characters =
                    new SortedList<string, uint>(scenarioResponse.Data.Content.Options.ToDictionary(x => x.Title, x => uint.Parse(x.Id.Substring(9))));
                for (int i = 0; i < characters.Count; i++)
                {
                    // Idk why adding this way can lead to unordered or repeated values
                    //characters.Add(options[i].Title, uint.Parse(options[i].Id.Substring(9))); // "scenario:xxxxxx"
                    list += $"**{i + 1}.** {characters.ElementAt(i).Key.ToTitleCase()}\n";
                }

                builder.Title = Locate("CharacterSelect");
                builder.Description = list;

                await ReplyAsync(embed: builder.Build());

                await userInput.TryDeleteAsync();

                userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(1));

                if (userInput == null)
                {
                    return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
                }
                if (int.TryParse(userInput.Content, out int characterOption))
                {
                    if (characterOption < 1 || characterOption > characters.Count)
                    {
                        return FergunResult.FromError($"{Locate("OutOfIndex")} {Locate("CreationCanceled")}");
                    }
                    characterOption--;
                }
                else
                {
                    characterOption = characters.IndexOfKey(userInput.Content.ToLowerInvariant());
                    if (characterOption == -1)
                    {
                        return FergunResult.FromError($"{Locate("InvalidOption")} {Locate("CreationCanceled")}");
                    }
                }

                builder.Title = "AI Dungeon";
                builder.Description = string.Format(Locate("GeneratingNewAdventure"), _modes.Keys[modeOption], characters.Keys[characterOption]);

                await ReplyAsync(embed: builder.Build());

                await userInput.TryDeleteAsync();

                ScenarioResponse scenario;
                try
                {
                    scenario = await API.GetScenarioAsync(characters.Values[characterOption]);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(scenario);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                CreationResponse response;
                try
                {
                    response = await API.CreateAdventureAsync(characters.Values[characterOption], scenario.Data.Content.Prompt.Replace("${character.name}", Context.User.Username, StringComparison.OrdinalIgnoreCase));
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(response);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                id = uint.Parse(response.Data.AdventureInfo.ContentId, CultureInfo.InvariantCulture); // "adventure:xxxxxxx"
                historyList = response.Data.AdventureInfo.HistoryList;
            }
            else
            {
                builder.Title = Locate("CustomChararacterCreation");
                builder.Description = Locate("CustomCharacterPrompt");

                await ReplyAsync(embed: builder.Build());

                await userInput.TryDeleteAsync();

                userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(5));

                if (userInput == null)
                {
                    return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
                }

                string customText = userInput.Content;

                if (customText.Length > 140)
                {
                    return FergunResult.FromError($"{Locate("140CharsMax")} {Locate("CreationCanceled")}");
                }

                await userInput.TryDeleteAsync();

                builder.Title = "AI Dungeon";
                builder.Description = Locate("GeneratingNewCustomAdventure");

                await ReplyAsync(embed: builder.Build());

                CreationResponse adventure;
                try
                {
                    adventure = await API.CreateAdventureAsync(_modes.Values[modeOption]);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(adventure);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                if (AutoTranslate())
                {
                    customText = await TranslateSimplerAsync(customText, GetLanguage(), "en");
                }

                id = uint.Parse(adventure.Data.AdventureInfo.ContentId, CultureInfo.InvariantCulture);

                WebSocketActionResponse response = null;
                try
                {
                    response = await API.RunWebSocketActionAsync(id, ActionType.Story, customText);
                }
                catch (IOException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                catch (TimeoutException)
                {
                    return FergunResult.FromError(Locate("ErrorOnAPI"));
                }
                responseError = CheckResponse(response);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                historyList = response.Payload.Data.SubscribeContent.HistoryList;
            }

            // Seems that in a custom adventure the last 2 history will always be empty, so we need to filter that.
            historyList.RemoveAll(x => string.IsNullOrEmpty(x.Text));

            // This should prevent any errors
            if (historyList.Count == 0)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            string initialPrompt = historyList[historyList.Count - 1].Text;
            if (historyList.Count > 1)
            {
                string previousHistory = "";
                historyList.RemoveAt(historyList.Count - 1);
                foreach (var history in historyList)
                {
                    previousHistory += history.Text;
                }
                initialPrompt = previousHistory + initialPrompt;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(initialPrompt))
            {
                initialPrompt = await TranslateSimplerAsync(initialPrompt, "en", GetLanguage());
            }

            builder.Description = initialPrompt;
            builder.WithFooter($"ID: {id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}", IconUrl);

            await ReplyAsync(embed: builder.Build());

            FergunClient.Database.InsertRecord("AIDAdventures", new AidAdventure(id, Context.User.Id, false));
            return FergunResult.FromSuccess();
        }
        */

        private async Task<string> RunAIDCommandAsync(uint adventureId, string promptText, ActionType actionType = ActionType.Continue, string text = "", uint actionId = 0)
        {
            // check the id
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return checkResult;
            }

            // For the continue command
            if (actionType != ActionType.Continue)
            {
                if (string.IsNullOrEmpty(text))
                {
                    actionType = ActionType.Continue;
                }
                else
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
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription($"{FergunClient.LoadingEmote} {Locate(promptText)}")
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
                builder.Description = $"{FergunClient.LoadingEmote} {Locate("WaitingQueue")}";
            }
            await ReplyAsync(embed: builder.Build());

            await _queue[adventureId].WaitAsync();

            if (wasWaiting)
            {
                builder.Description = $"{FergunClient.LoadingEmote} {Locate(promptText)}";
                await ReplyAsync(embed: builder.Build());
            }

            // if a text is passed
            if (!string.IsNullOrEmpty(text) && AutoTranslate())
            {
                text = await TranslateSimplerAsync(text, GetLanguage(), "en");
            }

            // send action
            WebSocketActionResponse response;
            try
            {
                response = await API.RunWebSocketActionAsync(adventureId, actionType, text, actionId);
            }
            catch (IOException e)
            {
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
            string responseError = CheckResponse(response);
            if (responseError != null)
            {
                return responseError;
            }

            var historyList = response.Payload.Data.SubscribeContent.HistoryList;
            if (historyList.Count == 0)
            {
                return Locate("ErrorInAPI");
            }

            string textToShow = historyList[historyList.Count - 1].Text;
            if (actionType == ActionType.Do ||
                actionType == ActionType.Say ||
                actionType == ActionType.Story)
            {
                textToShow = historyList[historyList.Count - 2].Text + textToShow;
            }

            if (!string.IsNullOrEmpty(textToShow) && AutoTranslate())
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            if (actionType == ActionType.Remember)
            {
                builder.Description = $"{Locate("TheAIWillNowRemember")}\n{textToShow}";
            }
            else
            {
                builder.Description = textToShow;
            }
            builder = builder.WithFooter($"ID: {adventureId} - Tip: {GetTip()}", IconUrl);

            await ReplyAsync(embed: builder.Build());

            return null;
        }

        [Command("continue", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Continue(uint adventureId, [Remainder] string text = "")
        {
            string result = await RunAIDCommandAsync(adventureId, "GeneratingStory", text: text);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("undo", RunMode = RunMode.Async)]
        [Alias("revert")]
        public async Task<RuntimeResult> Undo(uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "RevertingLastAction", ActionType.Undo);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("redo", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Redo(uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "RedoingLastAction", ActionType.Redo);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("remember", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Remember(uint adventureId, [Remainder] string text)
        {
            string result = await RunAIDCommandAsync(adventureId, "EditingStoryContext", ActionType.Remember, text);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("alter", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Alter(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.LoadingEmote} {Locate("Loading")}")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            AdventureInfoResponse adventure;
            try
            {
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            string responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                return FergunResult.FromError(responseError);
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

            builder = builder
                .WithAuthor(Context.User)
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
        public async Task<RuntimeResult> Retry(uint adventureId)
        {
            string result = await RunAIDCommandAsync(adventureId, "GeneratingNewResponse", ActionType.Retry);
            if (result != null)
            {
                return FergunResult.FromError(result);
            }

            return FergunResult.FromSuccess();
        }

        [Command("makepublic")]
        public async Task<RuntimeResult> Makepublic(uint adventureId)
        {
            AidAdventure currentAdventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (currentAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != currentAdventure.OwnerID)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (currentAdventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPublic"));
            }

            currentAdventure.IsPublic = true;
            FergunClient.Database.UpdateRecord("AIDAdventures", currentAdventure);
            await SendEmbedAsync(Locate("IDNowPublic"));
            return FergunResult.FromSuccess();
        }

        [Command("makeprivate")]
        public async Task<RuntimeResult> Makeprivate(uint adventureId)
        {
            AidAdventure currentAdventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (currentAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != currentAdventure.OwnerID)
            {
                return FergunResult.FromError(Locate("NotIDOwner"));
            }
            if (!currentAdventure.IsPublic)
            {
                return FergunResult.FromError(Locate("IDAlreadyPrivate"));
            }

            currentAdventure.IsPublic = false;
            FergunClient.Database.UpdateRecord("AIDAdventures", currentAdventure);
            await SendEmbedAsync(Locate("IDNowPrivate"));
            return FergunResult.FromSuccess();
        }

        [Command("idlist", RunMode = RunMode.Async)]
        [Alias("ids", "list")]
        public async Task<RuntimeResult> Idlist(IUser user = null)
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
        public async Task<RuntimeResult> Idinfo(uint adventureId)
        {
            AidAdventure currentAdventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);
            if (currentAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            AdventureInfoResponse adventureInfo = null;
            try
            {
                adventureInfo = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            string responseError = CheckResponse(adventureInfo);
            if (responseError != null)
            {
                return FergunResult.FromError(responseError);
            }

            var historyList = adventureInfo.Data.Content.HistoryList;
            string initialPrompt;
            if (historyList.Count == 0)
            {
                initialPrompt = "???";
            }
            else
            {
                initialPrompt = historyList[0].Text;
                if (historyList.Count > 1)
                {
                    initialPrompt += historyList[1].Text;
                }
            }

            var idOwner = Context.IsPrivate ? null : Context.Guild.GetUser(currentAdventure.OwnerID);

            var builder = new EmbedBuilder()
                .WithTitle(Locate("IDInfo"))
                .WithDescription(initialPrompt)
                .AddField(Locate("IsPublic"), Locate(currentAdventure.IsPublic), true)
                .AddField(Locate("Owner"), idOwner?.ToString() ?? Locate("NotAvailable"), true)
                .WithFooter($"ID: {adventureId} - {Locate("CreatedAt")}:", IconUrl)
                .WithTimestamp(historyList[0].CreatedAt)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("delete", RunMode = RunMode.Async)]
        [Alias("remove")]
        public async Task<RuntimeResult> Delete(uint adventureId)
        {
            AidAdventure currentAdventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (currentAdventure == null)
            {
                return FergunResult.FromError(Locate("IDNotFound"));
            }
            if (Context.User.Id != currentAdventure.OwnerID)
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
                    //// await Context.Channel.TriggerTypingAsync();
                    var response = await API.DeleteAdventureAsync(adventureId);
                    string result;
                    if (response.Errors != null)
                    {
                        result = response.Errors[0].Message;
                    }
                    else if (response.Data == null)
                    {
                        result = Locate("AnErrorOccurred");
                    }
                    else
                    {
                        FergunClient.Database.DeleteRecord("AIDAdventures", currentAdventure);
                        result = Locate("AdventureDeleted");
                    }
                    await message.ModifyAsync(x => x.Embed = builder.WithDescription(result).Build());
                });

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();
        }

        [Command("dump", RunMode = RunMode.Async), Ratelimit(1, 20, Measure.Minutes)]
        public async Task<RuntimeResult> Dump(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            var builder = new EmbedBuilder()
                .WithDescription($"{FergunClient.LoadingEmote} {Locate("DumpingAdventure")}")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            AdventureInfoResponse adventure;
            try
            {
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            string responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                return FergunResult.FromError(responseError);
            }

            var historyList = adventure.Data.Content.HistoryList;

            if (historyList.Count == 0)
            {
                return FergunResult.FromError(Locate("ErrorOnAPI"));
            }

            var response = await Hastebin.UploadAsync(string.Join("", historyList.Select(x => x.Text)));

            builder.Description = Format.Url(Locate("HastebinLink"), response.GetLink());
            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        private async Task<string> CheckIdAsync(uint adventureId)
        {
            AidAdventure currentAdventure = FergunClient.Database.Find<AidAdventure>("AIDAdventures", x => x.ID == adventureId);

            if (currentAdventure == null)
            {
                return Locate("IDNotFound");
            }
            if (!currentAdventure.IsPublic && Context.User.Id != currentAdventure.OwnerID)
            {
                return string.Format(Locate("IDNotPublic"), (await Context.Client.Rest.GetUserAsync(currentAdventure.OwnerID)).ToString());
            }
            //if (WaitList.Contains(adventureId))
            //{
            //    return GetValue("IDOnWait");
            //}
            return null;
        }

        private string CheckResponse(WebSocketActionResponse response)
        {
            var content = response?.Payload?.Data?.SubscribeContent;
            if (content == null)
            {
                return Locate("ErrorOnAPI");
            }
            if (content.Error != null)
            {
                return content.Error.Message;
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
            var tips = Locate("AIDTips").Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
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
            return GetGuild()?.AidAutoTranslate ?? FergunConfig.AidAutoTranslateDefault;
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