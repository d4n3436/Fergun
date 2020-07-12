using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs.AIDungeon;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using GoogleTranslateFreeApi;

namespace Fergun.Modules
{
    [Group("aid"), Ratelimit(2, FergunClient.GlobalCooldown, Measure.Minutes)]
    public class AIDungeon : FergunBase
    {
        private static APIs.AIDungeon.AIDungeon API;
        //private static readonly Dictionary<uint, Queue<ulong>> _userQueue = new Dictionary<uint, Queue<ulong>>();
        private static readonly List<uint> _waitList = new List<uint>();
        private static readonly Random _rng = new Random();
        private static ScenarioResponse _modeList;
        private static bool _isModeListCached = false;

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
                .WithFooter(Locate("HelpFooter2"))
                //.AddField("Tips", "- " + string.Join("\n- ", GetValue("AIDTips").Split(new string[] { Environment.NewLine }, StringSplitOptions.None)))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("new", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> New()
        {
            //Console.WriteLine($"Executing new in {(Context.IsPrivate ? "DM" : $"{Context.Guild.Name}/{Context.Channel.Name}")} for {Context.User}");
            string list = $"\u2139 {string.Format(Locate("ModeSelect"), GetPrefix())}\n";
            string responseError;
            if (!_isModeListCached)
            {
                await Context.Channel.TriggerTypingAsync();
                try
                {
                    _modeList = await API.GetScenarioAsync(APIs.AIDungeon.AIDungeon.AllScenarios);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(_modeList);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }
                _isModeListCached = true;
            }

            SortedList<string, uint> modes = new SortedList<string, uint>();
            foreach (var mode in _modeList.Data.Content.Options)
            {
                modes.Add(mode.Title, uint.Parse(mode.Id.Substring(9))); // "scenario:xxxxxx"
            }

            for (int i = 0; i < modes.Count; i++)
            {
                var current = modes.ElementAt(i).Key;
                list += $"**{i + 1}.** {current.ToTitleCase()}\n";
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(Locate("AIDungeonWelcome"))
                .WithDescription(list)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            SocketMessage userInput;
            userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(1));

            if (userInput == null)
            {
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
            }
            if (!int.TryParse(userInput.Content, out int modeOption))
            {
                if (userInput.Content.ToLowerInvariant() == $"{GetPrefix()}aid info")
                {
                    return FergunResult.FromSuccess();
                }
                modeOption = modes.IndexOfKey(userInput.Content.ToLowerInvariant());
                if (modeOption == -1)
                {
                    return FergunResult.FromError($"{Locate("InvalidOption")} {Locate("CreationCanceled")}");
                }
                modeOption++;
            }
            else if (modeOption < 1 || modeOption > modes.Count)
            {
                return FergunResult.FromError($"{Locate("OutOfIndex")} {Locate("CreationCanceled")}");
            }

            modeOption--;
            var builder3 = new EmbedBuilder();

            List<History> historyList;
            uint id;

            if (modes.Keys[modeOption] != "custom")
            {
                ScenarioResponse characterList;
                try
                {
                    characterList = await API.GetScenarioAsync(modes.Values[modeOption]);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(characterList);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                SortedList<string, uint> characters = new SortedList<string, uint>();
                foreach (var character in characterList.Data.Content.Options)
                {
                    characters.Add(character.Title, uint.Parse(character.Id.Substring(9))); // "scenario:xxxxxx"
                }

                list = "";
                for (int i = 0; i < characters.Count; i++)
                {
                    var current = characters.Keys[i];
                    list += $"**{i + 1}.** {current.ToTitleCase()}\n";
                }

                var builder2 = new EmbedBuilder()
                    .WithAuthor(Context.User)
                    .WithTitle(Locate("CharacterSelect"))
                    .WithDescription(list)
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder2.Build());

                await userInput.TryDeleteAsync();

                userInput = await NextMessageAsync(true, true, TimeSpan.FromMinutes(1));

                if (userInput == null)
                {
                    return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
                }
                if (!int.TryParse(userInput.Content, out int characterOption))
                {
                    characterOption = characters.IndexOfKey(userInput.Content.ToLowerInvariant());
                    if (characterOption == -1)
                    {
                        return FergunResult.FromError($"{Locate("InvalidOption")} {Locate("CreationCanceled")}");
                    }
                    characterOption++;
                }
                else if (characterOption < 1 || characterOption > characters.Count)
                {
                    return FergunResult.FromError($"{Locate("OutOfIndex")} {Locate("CreationCanceled")}");
                }

                characterOption--;

                builder3.WithAuthor(Context.User)
                    .WithTitle("AI Dungeon")
                    .WithDescription(string.Format(Locate("GeneratingNewAdventure"), modes.Keys[modeOption], characters.Keys[characterOption]))
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder3.Build());

                await userInput.TryDeleteAsync();
                // await Context.Channel.TriggerTypingAsync();

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
                AdventureInfoResponse adventure;
                try
                {
                    response = await API.CreateAdventureAsync(characters.Values[characterOption], scenario.Data.Content.Prompt.Replace("${character.name}", Context.User.Username, StringComparison.OrdinalIgnoreCase));
                    id = uint.Parse(response.Data.AdventureInfo.ContentId, CultureInfo.InvariantCulture); // "adventure:xxxxxxx"
                    await Task.Delay(15000);
                    adventure = await API.GetAdventureAsync(id);
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
                responseError = CheckResponse(adventure);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }
                if (adventure.Data.Content.HistoryList.Count == 0)
                {
                    // Generally happens when the token is invalid
                    return FergunResult.FromError(Locate("ErrorOnAPI"));
                }

                historyList = adventure.Data.Content.HistoryList; // result.Data.AdventureInfo.HistoryList.Count - 1
            }
            else
            {
                var builder2 = new EmbedBuilder()
                    .WithAuthor(Context.User)
                    .WithTitle(Locate("CustomChararacterCreation"))
                    .WithDescription(Locate("CustomCharacterPrompt"))
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder2.Build());

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

                builder3.WithAuthor(Context.User)
                    .WithTitle("AI Dungeon")
                    .WithDescription(Locate("GeneratingNewCustomAdventure"))
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder3.Build());

                CreationResponse adventure;
                try
                {
                    adventure = await API.CreateAdventureAsync(modes.Values[modeOption]);
                    await Task.Delay(10000);
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

                ActionResponse action;
                AdventureInfoResponse response;
                try
                {
                    action = await API.RunActionAsync(id, APIs.AIDungeon.ActionType.Describe, customText);

                    await Task.Delay(10000);
                    response = await API.GetAdventureAsync(id);
                }
                catch (HttpRequestException e)
                {
                    return FergunResult.FromError(e.Message);
                }
                responseError = CheckResponse(action);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }
                responseError = CheckResponse(response);
                if (responseError != null)
                {
                    return FergunResult.FromError(responseError);
                }

                historyList = response.Data.Content.HistoryList; // Action.Data.UserAction.HistoryList.Count - 1
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

            var builder4 = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(initialPrompt)
                .WithFooter($"ID: {id} - Tip: {string.Format(Locate("FirstTip"), GetPrefix())}")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder4.Build());

            FergunClient.Database.InsertRecord("AIDAdventures", new AidAdventure(id, Context.User.Id, false));
            return FergunResult.FromSuccess();
        }

        [Command("continue", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Continue(uint adventureId, [Remainder] string text = "")
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }
            //if (_userQueue.ContainsKey(adventureId))
            //{
            //}
            if (_waitList.Contains(adventureId))
            {
                await SendEmbedAsync(Locate("WaitingQueue"), true);
                while (_waitList.Contains(adventureId))
                {
                    await Task.Delay(25);
                }
            }

            _waitList.Add(adventureId);

            APIs.AIDungeon.ActionType actionType;

            if (string.IsNullOrEmpty(text))
            {
                actionType = APIs.AIDungeon.ActionType.Continue;
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
                    if (actionType != APIs.AIDungeon.ActionType.Do
                        && actionType!= APIs.AIDungeon.ActionType.Say
                        && actionType != APIs.AIDungeon.ActionType.Describe)
                    {
                        actionType = APIs.AIDungeon.ActionType.Do;
                    }
                }
                else
                {
                    if (splitText[0].ToLowerInvariant() == "story")
                    {
                        actionType = APIs.AIDungeon.ActionType.Describe;
                    }
                    else
                    {
                        // if the parse fails, keep the text and set the input type to Do (the default).
                        actionType = APIs.AIDungeon.ActionType.Do;
                    }
                }
            }

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(Locate("GeneratingStory"))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            if (AutoTranslate() && !string.IsNullOrEmpty(text))
            {
                text = await TranslateSimplerAsync(text, GetLanguage(), "en");
            }

            ActionResponse action = null;
            AdventureInfoResponse adventure = null;
            try
            {
                action = await API.RunActionAsync(adventureId, actionType, text);

                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(action);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }
            responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            var historyList = adventure.Data.Content.HistoryList;
            var lastHistory = historyList[historyList.Count - 1];
            string textToShow = lastHistory.Text;
            if (actionType != APIs.AIDungeon.ActionType.Continue)
            {
                textToShow = historyList[historyList.Count - 2].Text + textToShow;
            }

            if (AutoTranslate() && !string.IsNullOrEmpty(textToShow))
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            builder = builder
                .WithDescription(textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}");
            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("undo", RunMode = RunMode.Async)]
        [Alias("revert")]
        public async Task<RuntimeResult> Undo(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            _waitList.Add(adventureId);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(Locate("RevertingLastAction"))
                .WithColor(FergunConfig.EmbedColor);

            var msg = await ReplyAsync(embed: builder.Build());

            // await Context.Channel.TriggerTypingAsync();

            ActionResponse action = null;
            AdventureInfoResponse adventure = null;
            try
            {
                action = await API.RunActionAsync(adventureId, APIs.AIDungeon.ActionType.Undo);
                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(action);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }
            responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            var historyList = adventure.Data.Content.HistoryList;
            string textToShow = historyList[historyList.Count - 1].Text;

            if (AutoTranslate())
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            await msg.ModifyAsync(x =>
            {
                x.Embed = builder
                .WithDescription(textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}")
                .Build();
            });

            return FergunResult.FromSuccess();
        }

        [Command("redo", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Redo(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            _waitList.Add(adventureId);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(Locate("RedoingLastAction"))
                .WithColor(FergunConfig.EmbedColor);

            var msg = await ReplyAsync(embed: builder.Build());

            // await Context.Channel.TriggerTypingAsync();

            ActionResponse action = null;
            AdventureInfoResponse adventure = null;
            try
            {
                action = await API.RunActionAsync(adventureId, APIs.AIDungeon.ActionType.Redo);
                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(action);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }
            responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            var historyList = adventure.Data.Content.HistoryList;
            string textToShow = historyList[historyList.Count - 1].Text;

            if (AutoTranslate())
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            await msg.ModifyAsync(x =>
            {
                x.Embed = builder
                .WithDescription(textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}")
                .Build();
            });

            return FergunResult.FromSuccess();
        }

        [Command("remember", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Remember(uint adventureId, [Remainder] string text)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            _waitList.Add(adventureId);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(Locate("EditingStoryContext"))
                .WithColor(FergunConfig.EmbedColor);

            var msg = await ReplyAsync(embed: builder.Build());

            // Show the translated text or the original in the embed?
            if (AutoTranslate())
            {
                text = await TranslateSimplerAsync(text, GetLanguage(), "en");
            }

            ActionResponse action = null;
            AdventureInfoResponse adventure = null;
            try
            {
                action = await API.RunActionAsync(adventureId, APIs.AIDungeon.ActionType.Remember, text);
                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(action);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }
            responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            await msg.ModifyAsync(x =>
            {
                x.Embed = builder
                .WithDescription($"{Locate("TheAIWillNowRemember")}\n{text}")
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}")
                .Build();
            });

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

            _waitList.Add(adventureId);

            var builder = new EmbedBuilder()
                .WithDescription("Loading...")
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
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
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
                return FergunResult.FromError($"{Locate("SearchTimeout")} {Locate("CreationCanceled")}");
            }

            string newOutput = userInput.Content.Trim();

            if (newOutput.Length > 140)
            {
                return FergunResult.FromError($"{Locate("140CharsMax")} {Locate("CreationCanceled")}");
            }

            await userInput.TryDeleteAsync();

            builder.Description = Locate("Loading");

            await ReplyAsync(embed: builder.Build());

            _waitList.Add(adventureId);

            if (AutoTranslate())
            {
                newOutput = await TranslateSimplerAsync(newOutput, GetLanguage(), "en");
            }

            ActionResponse actionResponse;
            adventure = null;
            try
            {
                actionResponse = await API.RunActionAsync(adventureId, APIs.AIDungeon.ActionType.Alter, newOutput, actionId);
                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            responseError = CheckResponse(actionResponse);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            var lastHistory = adventure.Data.Content.HistoryList[adventure.Data.Content.HistoryList.Count - 1];
            string textToShow = lastHistory.Text;

            if (AutoTranslate() && !string.IsNullOrEmpty(textToShow))
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            builder = builder
                .WithDescription(textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}");
            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("retry", RunMode = RunMode.Async)]
        public async Task<RuntimeResult> Retry(uint adventureId)
        {
            string checkResult = await CheckIdAsync(adventureId);
            if (checkResult != null)
            {
                return FergunResult.FromError(checkResult);
            }

            _waitList.Add(adventureId);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle("AI Dungeon")
                .WithDescription(Locate("GeneratingNewResponse"))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            ActionResponse action = null;
            AdventureInfoResponse adventure = null;
            try
            {
                action = await API.RunActionAsync(adventureId, APIs.AIDungeon.ActionType.Retry);
                await Task.Delay(10000);
                adventure = await API.GetAdventureAsync(adventureId);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
            }
            finally
            {
                _waitList.Remove(adventureId);
            }
            string responseError = CheckResponse(action);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }
            responseError = CheckResponse(adventure);
            if (responseError != null)
            {
                _waitList.Remove(adventureId);
                return FergunResult.FromError(responseError);
            }

            var lastHistory = adventure.Data.Content.HistoryList[adventure.Data.Content.HistoryList.Count - 1];
            string textToShow = lastHistory.Text;

            if (AutoTranslate() && !string.IsNullOrEmpty(textToShow))
            {
                textToShow = await TranslateSimplerAsync(textToShow, "en", GetLanguage());
            }

            builder = builder
                .WithDescription(textToShow)
                .WithFooter($"ID: {adventureId} - Tip: {GetTip()}");
            await ReplyAsync(embed: builder.Build());

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
                .WithFooter(Locate("IDListFooter"))
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
                .WithFooter($"ID: {adventureId} - {Locate("CreatedAt")}:")
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
                        result = Locate("AnErrorOcurred");
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

        private static string CheckResponse(IResponse response)
        {
            // Invalid response content
            //if (Response == null)
            //{
            //    return Locate("ErrorOnAPI");
            //}
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
                    var result = await APIs.Translators.TranslateBingAsync(text, to, from);
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