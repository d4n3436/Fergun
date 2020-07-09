using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Fergun.APIs.OpenTriviaDB;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Fergun.Modules
{
    [Ratelimit(3, FergunClient.GlobalCooldown, Measure.Minutes)]
    public class Other : FergunBase
    {
        private static readonly string[] categories = {
            "general_knowledge",
            "books",
            "film",
            "music",
            "musicals_theatres",
            "television",
            "video_games",
            "board_games",
            "science",
            "computers",
            "mathematics",
            "mythology",
            "sports",
            "geography",
            "history",
            "politics",
            "art",
            "celebrities",
            "animals",
            "vehicles",
            "comics",
            "gadgets",
            "anime",
            "cartoons"
        };

        private static readonly string[] difficulties = { "Any", "easy", "medium", "hard" };

        private static readonly string[] responseCodes = { "Success", "No Results", "Invalid Parameter", "Token Not Found", "Token Empty", "Unknown Error" };

        [ThreadStatic]
        private static Random _rngInstance;

        private readonly CommandService _cmdService;
        public Other(CommandService commands)
        {
            _cmdService = commands;
        }

        private static Random RngInstance => _rngInstance ??= new Random();
        //private static readonly List<ulong> WaitList = new List<ulong>();
        [Command("changelog")]
        [Summary("changelogSummary")]
        [Alias("update")]
        public async Task<RuntimeResult> Changelog([Summary("changelogParam1")] string version = null)
        {
            string[] versions = Locate("Versions").Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            version ??= Locate("Version");
            if (versions.FirstOrDefault(x => x == version) == null)
            {
                return FergunResult.FromError(string.Format(Locate("VersionNotFound"), string.Join(", ", versions)));
            }

            var builder = new EmbedBuilder()
                .WithTitle("Fergun Changelog")
                .AddField($"v{version}", Locate($"Changelog{version}"))
                .WithFooter(string.Format(Locate("OtherVersions"), string.Join(", ", versions.Where(x => x != version))))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [Command("code", RunMode = RunMode.Async), Ratelimit(1, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("codeSummary")]
        [Alias("source")]
        public async Task<RuntimeResult> Code([Summary("codeParam1")] string commandName = null)
        {
            // maybe some day
            bool isDisabled = true;
            if (isDisabled)
            {
                return FergunResult.FromError("In maintenance.");
            }
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != "Dev");
            if (command == null)
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }
            string methodName = char.ToUpperInvariant(command.Name[0]) + string.Join("", command.Name.Skip(1));

            DirectoryInfo workingDir = new DirectoryInfo(Environment.CurrentDirectory);
            DirectoryInfo sourceDir = workingDir.Parent.Parent.Parent;

            string path = $"{sourceDir.FullName}\\Modules\\{(command.Module.Name == "aid" ? "AIDungeon" : command.Module.Name)}.cs";

            string methodBody;

            using (var stream = File.OpenRead(path))
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: path);
                var root = syntaxTree.GetRoot();
                var method = root.DescendantNodes()
                                 .OfType<MethodDeclarationSyntax>()
                                 .Where(md => md.Identifier.ValueText == methodName)
                                 .FirstOrDefault().NormalizeWhitespace();
                methodBody = method.ToString().Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            await PagedReplyAsync(methodBody.SplitToLines(EmbedBuilder.MaxDescriptionLength - 9).Select(x => $"```cs\n{x}```"));

            return FergunResult.FromSuccess();
        }

        [Command("cringe")]
        [Summary("cringeSummary")]
        public async Task Cringe()
        {
            await ReplyAsync("https://fergun.is-inside.me/MyAxVm6x.mp4");
        }

        [Command("inspirobot")]
        [Summary("inspirobotSummary")]
        public async Task Inspirobot()
        {
            string img;
            using (WebClient wc = new WebClient())
            {
                img = await wc.DownloadStringTaskAsync("https://inspirobot.me/api?generate=true"); //&oy=vey
            }
            var builder = new EmbedBuilder()
                .WithTitle("InspiroBot")
                .WithImageUrl(img)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("invite")]
        [Summary("inviteSummary")]
        public async Task Invite()
        {
            await SendEmbedAsync(Format.Url(Locate("InviteLink"), FergunClient.InviteLink));
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer", Group = "Permission")]
        [RequireOwner(Group = "Permission")]
        [Command("language")]
        [Summary("languageSummary")]
        [Alias("lang")]
        public async Task<RuntimeResult> Language()
        {
            var currentGuild = GetGuild() ?? new Guild(Context.Guild.Id);
            string currentLanguage = currentGuild?.Language ?? FergunConfig.DefaultLanguage;

            bool hasReacted = false;
            IUserMessage message = null;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("LanguageSelection"))
                .WithDescription(Locate("LanguagePrompt"))
                .WithColor(FergunConfig.EmbedColor);

            async Task HandleLanguageUpdateAsync(string newLanguage)
            {
                if (hasReacted || currentLanguage == newLanguage)
                {
                    return;
                }
                hasReacted = true;
                currentGuild.Language = newLanguage;
                FergunClient.Database.UpdateRecord("Guilds", currentGuild);
                await message.ModifyAsync(x => x.Embed = builder.WithTitle(Locate("LanguageSelection")).WithDescription($"✅ {Locate("NewLanguage")}").Build());
            }

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(1),
                async (_) =>
                {
                    if (!hasReacted)
                    {
                        await message.ModifyAsync(x => x.Embed = builder.WithDescription($"❌ {Locate("ReactTimeout")}").Build());
                    }
                });

            foreach (var lang in FergunClient.Languages)
            {
                data = data.AddCallBack(new Emoji(lang.Value), async (_, _1) => await HandleLanguageUpdateAsync(lang.Key));
            }

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();
        }

        [Command("nothing")]
        [Summary("nothingSummary")]
        [Alias("noop")]
        public async Task Nothing()
        {
            ;
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("prefix")]
        [Summary("prefixSummary")]
        [Alias("setprefix")]
        public async Task<RuntimeResult> Prefix([Summary("prefixParam1")] string newPrefix)
        {
            if (FergunClient.IsDebugMode)
            {
                return FergunResult.FromError("No");
            }

            if (newPrefix == GetPrefix())
            {
                return FergunResult.FromError(Locate("PrefixSameCurrentTarget"));
            }
            if (newPrefix.Length > 10)
            {
                return FergunResult.FromError(Locate("PrefixTooLong"));
            }
            // null prefix = use the global prefix
            var currentGuild = GetGuild() ?? new Guild(Context.Guild.Id);
            if (newPrefix == FergunConfig.GlobalPrefix)
            {
                currentGuild.Prefix = null; //Default prefix
            }
            else
            {
                currentGuild.Prefix = newPrefix;
            }

            FergunClient.Database.UpdateRecord("Guilds", currentGuild);
            await SendEmbedAsync($"{Locate("NewPrefix")} \"{newPrefix}\"");
            return FergunResult.FromSuccess();
        }

        [Command("reaction")]
        [Summary("reactionSummary")]
        [Alias("react")]
        public async Task<RuntimeResult> Reaction([Summary("reactionParam1")] string reaction,
            [Summary("reactionParam2")] ulong? messageId = null)
        {
            IMessage msg;
            if (messageId == null)
            {
                msg = Context.Message;
            }
            else
            {
                msg = await Context.Channel.GetMessageAsync(messageId.Value);
                if (msg == null)
                {
                    return FergunResult.FromError(Locate("InvalidMessageID"));
                }
            }
            try
            {
                IEmote emote;
                if (reaction.Length > 2)
                {
                    emote = Emote.Parse(reaction);
                }
                else
                {
                    emote = new Emoji(reaction);
                }
                await msg.AddReactionAsync(emote);
                return FergunResult.FromSuccess();
            }
            catch (ArgumentException) // Invalid emote format. (Parameter 'text')
            {
                return FergunResult.FromError(Locate("InvalidReaction"));
            }
            catch (Discord.Net.HttpException) // The server responded with error 400: BadRequest
            {
                return FergunResult.FromError(Locate("InvalidReaction"));
            }
        }

        [Command("say", RunMode = RunMode.Async)]
        [Summary("saySummary")]
        public async Task Say([Remainder, Summary("sayParam1")] string text)
        {
            await ReplyAsync(text, allowedMentions: AllowedMentions.None);
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("someone")]
        [Summary("someoneSummary")]
        public async Task Someone()
        {
            var user = Context.Guild.Users.ElementAt(RngInstance.Next(Context.Guild.Users.Count)); // Context.Guild.MemberCount may give the incorrect count
            await ReplyAsync(user.ToString());
        }

        [LongRunning]
        [Command("stats", RunMode = RunMode.Async)]
        [Summary("statsSummary")]
        [Alias("info", "botinfo")]
        public async Task Stats()
        {
            var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
            var cpuUsage = (int)await GetCpuUsageForProcessAsync();
            string cpu;
            string totalRamUsage;
            string processRamUsage;
            if (FergunClient.IsLinux)
            {
                cpu = "Intel Xeon Gold 6140 @ 4x 2.295GHz";

                string output = "free -m".Bash();
                var lines = output.Split("\n");
                var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                totalRamUsage = memory[2];

                int ProcessId = Process.GetCurrentProcess().Id;
                processRamUsage = $"ps -o rss= {ProcessId} | awk '{{printf \" % .0f\\n\", $1 / 1024}}'".Bash().TrimEnd();
            }
            else
            {
                cpu = "Intel Core i5-7400 CPU @ 3.00GHz";
                totalRamUsage = "?";
                processRamUsage = (Process.GetCurrentProcess().NonpagedSystemMemorySize64 / 1024).ToString();
            }

            //var ramUsage = Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024;

            int totalUsers = 0;
            foreach (var guild in Context.Client.Guilds)
            {
                totalUsers += guild.MemberCount;
            }
            string version = "v" + Locate("Version");
            if (FergunClient.IsDebugMode)
            {
                version += "-dev";
            }
            var elapsed = DateTime.UtcNow - FergunClient.Uptime; //process.StartTime.ToUniversalTime();

            var builder = new EmbedBuilder()
                .WithTitle("Fergun Stats")

                .AddField("Uptime", elapsed.ToShortForm2(), true)
                .AddField("\u200b", "\u200b", true)
                .AddField("CPU", cpu, true)

                .AddField(Locate("CPUUsage"), cpuUsage + "%", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("RAMUsage"), $"{processRamUsage}MB / {totalRamUsage}MB / 985MB", true) // TODO: Find way to get the total RAM on windows / linux

                .AddField(Locate("Library"), $"Discord.Net\nv{DiscordConfig.Version}", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("BotVersion"), version, true)

                .AddField(Locate("TotalServers"), Context.Client.Guilds.Count, true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("TotalUsers"), totalUsers, true)

                .AddField(Locate("WebsocketPing"), $"{Context.Client.Latency}ms", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("BotOwner"), owner.Mention, true);

            if (!FergunClient.IsDebugMode)
            {
                builder.AddField("Links",
                    string.Format(Locate("Links"),
                    FergunClient.InviteLink,
                    FergunClient.DblBotPage,
                    $"{FergunClient.DblBotPage}/vote",
                    FergunClient.SupportServer));
            }
            builder.WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("support")]
        [Summary("reportSummary")]
        [Alias("bugs", "contact", "report")]
        public async Task Support()
        {
            var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
            await SendEmbedAsync(string.Format(Locate("ContactInfo"), FergunClient.SupportServer, owner.ToString()));
        }

        [Command("tcdne")]
        [Summary("tcdneSummary")]
        [Alias("tcde")]
        public async Task Tcdne()
        {
            var builder = new EmbedBuilder
            {
                Title = Locate("tcdneSummary"),
                ImageUrl = $"https://thiscatdoesnotexist.com/?{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Color = new Color(FergunConfig.EmbedColor)
            };

            await ReplyAsync(embed: builder.Build());
        }

        [Command("tpdne")]
        [Summary("tpdneSummary")]
        [Alias("tpde")]
        public async Task Tpdne()
        {
            var builder = new EmbedBuilder
            {
                Title = Locate("tpdneSummary"),
                ImageUrl = $"https://www.thispersondoesnotexist.com/image?{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Color = new Color(FergunConfig.EmbedColor)
            };

            await ReplyAsync(embed: builder.Build());
        }

        [LongRunning]
        [Command("trivia", RunMode = RunMode.Async)]
        [Summary("triviaSummary")]
        public async Task<RuntimeResult> Trivia([Summary("triviaParam1")] string category = null)
        {
            if (!string.IsNullOrEmpty(category))
            {
                category = category.ToLowerInvariant();
            }
            var builder = new EmbedBuilder();
            if (category == "categories")
            {
                builder.WithTitle(Locate("CategoryList"))
                    .WithDescription(string.Join("\n", categories))
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder.Build());
                return FergunResult.FromSuccess();
            }
            if (category == "reset")
            {
                var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
                if (Context.User.Id != owner.Id)
                {
                    return FergunResult.FromError(Locate("BotOwnerOnly"));
                }
                List<TriviaPlayer> playerList = FergunClient.Database.LoadRecords<TriviaPlayer>("TriviaStats");
                foreach (var player in playerList)
                {
                    player.Points = 0;
                    FergunClient.Database.UpdateRecord("TriviaStats", player);
                }
                return FergunResult.FromSuccess();
            }

            TriviaPlayer currentPlayer = FergunClient.Database.Find<TriviaPlayer>("TriviaStats", x => x.ID == Context.User.Id);
            if (currentPlayer == null)
            {
                currentPlayer = new TriviaPlayer(Context.User.Id);
                FergunClient.Database.InsertRecord("TriviaStats", currentPlayer);
            }

            if (category == "leaderboard" || category == "ranks")
            {
                if (Context.IsPrivate)
                {
                    return FergunResult.FromError(Locate("NotSupportedInDM"));
                }
                string CurrentuserList = "";
                string CurrentpointsList = "";
                int CurrentUsers = 0;
                var PlayerList = FergunClient.Database.LoadRecords<TriviaPlayer>("TriviaStats").OrderByDescending(x => x.Points);
                foreach (var player in PlayerList.Take(15))
                {
                    var user = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, player.ID);
                    if (user != null)
                    {
                        CurrentUsers++;
                        CurrentuserList += $"{user}\n";

                        CurrentpointsList += $"{player.Points}\n";
                    }
                }

                builder.WithTitle(Locate("TriviaLeaderboard"))
                    .AddField(Locate("User"), CurrentUsers > 0 ? CurrentuserList : "?", true)
                    .AddField(Locate("Points"), CurrentUsers > 0 ? CurrentpointsList : "?", true)
                    .WithColor(FergunConfig.EmbedColor);
                await ReplyAsync(embed: builder.Build());

                return FergunResult.FromSuccess();
            }
            //if (WaitList.Contains(Context.User.Id))
            //{
            //    return FergunResult.FromSuccess();
            //}

            //WaitList.Add(Context.User.Id);
            int index = 0;
            if (category != null)
            {
                index = Array.FindIndex(categories, x => x == category);
                if (index <= -1)
                {
                    index = 0;
                }
            }
            //var trivia = Api.RequestQuestions(1, QuestionDifficulty.Undefined, QuestionType.Undefined, ResponseEncoding.url3986, category == null ? 0 : (uint)index + 9);
            var trivia = TriviaApi.RequestQuestions(1, category == null ? QuestionCategory.Any : (QuestionCategory)(index + 9), encoding: ResponseEncoding.url3986);

            if (trivia.ResponseCode != 0)
            {
                //WaitList.Remove(Context.User.Id);
                return FergunResult.FromError(trivia.ResponseCode == 4 ? Locate("AllQuestionsAnswered") : $"{Locate("TriviaError")} {trivia.ResponseCode}: {responseCodes[trivia.ResponseCode]}");
            }
            var question = trivia.Questions[0];

            List<string> options = new List<string>(question.IncorrectAnswers)
            {
                question.CorrectAnswer
            };

            options.Shuffle();

            string optionsText = "";
            for (int i = 0; i < options.Count; i++)
                optionsText += $"{i + 1}. {Uri.EscapeDataString(options[i])}\n";
            bool hasReacted = false;
            IUserMessage message = null;

            async Task HandleTriviaReactionAsync(string option)
            {
                builder = new EmbedBuilder();
                if (option == "__none")
                {
                    currentPlayer.Points--;
                    builder.Title = $"❌ {Locate("TimesUp")}";
                    builder.Description = Locate("Lost1Point");
                }
                else if (option == question.CorrectAnswer)
                {
                    currentPlayer.Points++;
                    builder.Title = $"✅ {Locate("CorrectAnswer")}";
                    builder.Description = Locate("Won1Point");
                }
                else
                {
                    currentPlayer.Points--;
                    builder.Title = $"❌ {Locate("Incorrect")}";
                    builder.Description = $"{Locate("Lost1Point")}\n{Locate("TheAnswerIs")} {Format.Code(Uri.EscapeDataString(question.CorrectAnswer))}";
                }

                builder.WithFooter($"{Locate("Points")}: {currentPlayer.Points}");
                builder.WithColor(FergunConfig.EmbedColor);
                FergunClient.Database.UpdateRecord("TriviaStats", currentPlayer);
                //WaitList.Remove(Context.User.Id);
                await message.ModifyAsync(x => x.Embed = builder.Build());
            }

            int time = (Array.IndexOf(difficulties, question.Difficulty) * 5) + (question.Type == "multiple" ? 10 : 5);

            builder.WithAuthor(Context.User)
               .WithTitle("Trivia")
               .AddField(Locate("Category"), Uri.EscapeDataString(question.Category), true)
               .AddField(Locate("Type"), Uri.EscapeDataString(question.Type), true)
               .AddField(Locate("Difficulty"), Uri.EscapeDataString(question.Difficulty), true)
               .AddField(Locate("Question"), Uri.EscapeDataString(question.Question))
               .AddField(Locate("Options"), optionsText)
               .WithFooter(string.Format(Locate("TimeLeft"), time))
               .WithColor(FergunConfig.EmbedColor);

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), true, true, TimeSpan.FromSeconds(time),
                async (_) =>
                {
                    if (!hasReacted)
                    {
                        await HandleTriviaReactionAsync("__none");
                    }
                });

            for (int i = 0; i < options.Count; i++)
            {
                string option = options[i];
                data = data.AddCallBack(new Emoji($"{i + 1}\ufe0f\u20e3"), async (_, _1) =>
                {
                    hasReacted = true;
                    await HandleTriviaReactionAsync(option);
                });
            }

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();
        }

        [Command("uptime")]
        [Summary("uptimeSummary")]
        public async Task Uptime()
        {
            var elapsed = DateTime.UtcNow - FergunClient.Uptime; //Process.GetCurrentProcess().StartTime.ToUniversalTime();

            var builder = new EmbedBuilder
            {
                Title = "Bot uptime",
                Description = elapsed.ToShortForm2(),
                Color = new Color(FergunConfig.EmbedColor)
            };

            await ReplyAsync(embed: builder.Build());
        }

        [Command("vote")]
        [Summary("voteSummary")]
        public async Task Vote()
        {
            if (FergunClient.IsDebugMode)
            {
                await ReplyAsync("No");
                return;
            }
            var builder = new EmbedBuilder
            {
                Description = string.Format(Locate("Vote"), $"{FergunClient.DblBotPage}/vote"),
                Color = new Color(FergunConfig.EmbedColor)
            };
            await ReplyAsync(embed: builder.Build());
        }

        private static async Task<double> GetCpuUsageForProcessAsync()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            await Task.Delay(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }
    }
}