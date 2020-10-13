using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Fergun.APIs.OpenTriviaDB;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Services;
using Fergun.Utils;

namespace Fergun.Modules
{
    [RequireBotPermission(Constants.MinimunRequiredPermissions)]
    [Ratelimit(3, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
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

        //[ThreadStatic]
        //private static Random _rngInstance;

        private static CommandService _cmdService;
        private static LogService _logService;

        public Other(CommandService commands, LogService logService)
        {
            _cmdService ??= commands;
            _logService ??= logService;
        }

        //private static Random RngInstance => _rngInstance ??= new Random();

        [Command("changelog")]
        [Summary("changelogSummary")]
        [Alias("update")]
        [Example("1.2")]
        public async Task<RuntimeResult> Changelog([Summary("changelogParam1")] string version = null)
        {
            version ??= Constants.Version;
            if (version != Constants.Version && Constants.PreviousVersions.FirstOrDefault(x => x == version) == null)
            {
                return FergunResult.FromError(string.Format(Locate("VersionNotFound"), string.Join(", ", Constants.PreviousVersions.Append(Constants.Version))));
            }

            var builder = new EmbedBuilder()
                .WithTitle("Fergun Changelog")
                //.AddField($"v{version}", Locate($"Changelog{version}"))
                .WithFooter(string.Format(Locate("OtherVersions"), string.Join(", ", Constants.PreviousVersions.Append(Constants.Version).Where(x => x != version))))
                .WithColor(FergunConfig.EmbedColor);

            var split = Locate($"Changelog{version}").SplitBySeparatorWithLimit('\n', EmbedFieldBuilder.MaxFieldValueLength).ToList();
            for (int i = 0; i < split.Count; i++)
            {
                builder.AddField(i == 0 ? $"v{version}" : "\u200b", split[i]);
            }

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [Command("code", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("codeSummary")]
        [Alias("source")]
        [Example("img")]
        public async Task<RuntimeResult> Code([Summary("codeParam1")] string commandName = null)
        {
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
            if (command == null)
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }

            // TODO: Get links pointing command methods from the GitHub repo.
            return FergunResult.FromError("WIP");
        }

        [Command("cmdstats", RunMode = RunMode.Async)]
        [Summary("cmdstatsSummary")]
        [Alias("commandstats")]
        public async Task<RuntimeResult> CmdStats()
        {
            var stats = FergunConfig.CommandStats.OrderByDescending(x => x.Value);
            int i = 1;
            string current = "";
            var pages = new List<PaginatedMessage.Page>();

            foreach (var pair in stats)
            {
                string command = $"{i}. {Format.Code(pair.Key)}: {pair.Value}\n";
                if (command.Length + current.Length > EmbedFieldBuilder.MaxFieldValueLength)
                {
                    pages.Add(new PaginatedMessage.Page { Description = current });
                    current = command;
                }
                else
                {
                    current += command;
                }
                i++;
            }
            if (!string.IsNullOrEmpty(current))
            {
                pages.Add(new PaginatedMessage.Page { Description = current });
            }
            if (pages.Count == 0)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            var pager = new PaginatedMessage()
            {
                Title = Locate("CommandStatsInfo"),
                Pages = pages,
                Color = new Color(FergunConfig.EmbedColor),
                Options = new PaginatedAppearanceOptions()
                {
                    FooterFormat = Locate("PaginatorFooter")
                }
            };

            await PagedReplyAsync(pager, ReactionList.Default);
            return FergunResult.FromSuccess();
        }

        [Command("cringe")]
        [Summary("cringeSummary")]
        public async Task Cringe()
        {
            await ReplyAsync("https://fergun.is-inside.me/MyAxVm6x.mp4");
        }

        [AlwaysEnabled]
        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("disable", RunMode = RunMode.Async)]
        [Summary("disableSummary")]
        [Example("img")]
        public async Task<RuntimeResult> Disable([Remainder, Summary("disableParam1")] string commandName)
        {
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
            if (command != null)
            {
                if (command.Attributes.Concat(command.Module.Attributes).Any(x => x is AlwaysEnabledAttribute))
                {
                    return FergunResult.FromError(string.Format(Locate("NonDisableable"), Format.Code(command.Name)));
                }
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }

            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            guild.DisabledCommands ??= new List<string>();
            if (guild.DisabledCommands.Contains(command.Name))
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyDisabled"), Format.Code(command.Name)));
            }

            guild.DisabledCommands.Add(command.Name);
            FergunClient.Database.UpdateRecord("Guilds", guild);
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Disable: Disabled command \"{command.Name}\" in server {Context.Guild.Id}."));

            await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandDisabled"), Format.Code(command.Name)));

            return FergunResult.FromSuccess();
        }

        [AlwaysEnabled]
        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("enable", RunMode = RunMode.Async)]
        [Summary("enableSummary")]
        [Example("img")]
        public async Task<RuntimeResult> Enable([Remainder, Summary("enableParam1")] string commandName)
        {
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
            if (command != null)
            {
                if (command.Attributes.Concat(command.Module.Attributes).Any(x => x is AlwaysEnabledAttribute))
                {
                    return FergunResult.FromError(string.Format(Locate("AlreadyEnabled"), Format.Code(command.Name)));
                }
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }

            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            guild.DisabledCommands ??= new List<string>();
            if (guild.DisabledCommands.Contains(command.Name))
            {
                guild.DisabledCommands.Remove(command.Name);
                FergunClient.Database.UpdateRecord("Guilds", guild);
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Enable: Enabled command \"{command.Name}\" in server {Context.Guild.Id}."));

                await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandEnabled"), Format.Code(command.Name)));
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyEnabled"), Format.Code(command.Name)));
            }

            return FergunResult.FromSuccess();
        }

        [Command("inspirobot")]
        [Summary("inspirobotSummary")]
        public async Task Inspirobot()
        {
            string img;
            using (WebClient wc = new WebClient())
            {
                img = await wc.DownloadStringTaskAsync("https://inspirobot.me/api?generate=true");
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Inspirobot: Generated url: {img}"));

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
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("language")]
        [Summary("languageSummary")]
        [Alias("lang")]
        public async Task<RuntimeResult> Language()
        {
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);

            bool hasReacted = false;
            IUserMessage message = null;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("LanguageSelection"))
                .WithDescription(Locate("LanguagePrompt"))
                .WithColor(FergunConfig.EmbedColor);

            async Task HandleLanguageUpdateAsync(string newLanguage)
            {
                if (hasReacted || guild.Language == newLanguage)
                {
                    return;
                }
                hasReacted = true;
                guild.Language = newLanguage;
                FergunClient.Database.UpdateRecord("Guilds", guild);

                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Language: Updated language to: \"{newLanguage}\" in {Context.Guild.Name}"));
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

            foreach (var lang in Constants.Languages)
            {
                data.AddCallBack(new Emoji(lang.Value), async (_, _1) => await HandleLanguageUpdateAsync(lang.Key));
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

        [AlwaysEnabled]
        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("prefix")]
        [Summary("prefixSummary")]
        [Alias("setprefix")]
        [Example("!")]
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
            if (newPrefix.Length > Constants.MaxPrefixLength)
            {
                return FergunResult.FromError(Locate("PrefixTooLong"));
            }

            // null prefix = use the global prefix
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            if (newPrefix == FergunConfig.GlobalPrefix)
            {
                guild.Prefix = null;
            }
            else
            {
                guild.Prefix = newPrefix;
            }

            FergunClient.Database.UpdateRecord("Guilds", guild);
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Language: Updated prefix to: \"{newPrefix}\" in {Context.Guild.Name}"));

            await SendEmbedAsync(string.Format(Locate("NewPrefix"), newPrefix));
            return FergunResult.FromSuccess();
        }

        [Command("reaction")]
        [Summary("reactionSummary")]
        [Alias("react")]
        [Example(":thonk: 635356699263887700")]
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
                    if (!Emote.TryParse(reaction, out Emote tempEmote))
                    {
                        return FergunResult.FromError(Locate("InvalidReaction"));
                    }
                    emote = tempEmote;
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
        [Example("hey")]
        public async Task Say([Remainder, Summary("sayParam1")] string text)
        {
            await ReplyAsync(text, allowedMentions: AllowedMentions.None);
        }

        //[RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        //[Command("someone")]
        //[Summary("someoneSummary")]
        //public async Task Someone()
        //{
        //    var user = Context.Guild.Users.ElementAt(RngInstance.Next(Context.Guild.Users.Count)); // Context.Guild.MemberCount may give the incorrect count
        //    await ReplyAsync(user.ToString());
        //}

        [LongRunning]
        [Command("stats", RunMode = RunMode.Async)]
        [Summary("statsSummary")]
        [Alias("info", "botinfo")]
        public async Task Stats()
        {
            long temp;
            var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
            var cpuUsage = (int)await CommandUtils.GetCpuUsageForProcessAsync();
            string cpu = null;
            long? totalRamUsage = null;
            long? processRamUsage = null;
            long? totalRam = null;
            string os = RuntimeInformation.OSDescription;
            if (FergunClient.IsLinux)
            {
                // CPU Name
                if (File.Exists("/proc/cpuinfo"))
                {
                    var cpuinfo = File.ReadAllLines("/proc/cpuinfo");
                    cpu = cpuinfo.ElementAtOrDefault(4)?.Split(':')?.ElementAtOrDefault(1);
                }

                // OS Name
                if (File.Exists("/etc/lsb-release"))
                {
                    var distroInfo = File.ReadAllLines("/etc/lsb-release");
                    os = distroInfo.ElementAtOrDefault(3)?.Split('=')?.ElementAtOrDefault(1)?.Trim('\"');
                }

                // Total RAM & total RAM usage
                var output = "free -m".RunCommand()?.Split(Environment.NewLine);
                var memory = output?.ElementAtOrDefault(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (long.TryParse(memory?.ElementAtOrDefault(1), out temp)) totalRam = temp;
                if (long.TryParse(memory?.ElementAtOrDefault(2), out temp)) totalRamUsage = temp;

                // Process RAM usage
                int processId = Process.GetCurrentProcess().Id;
                if (long.TryParse($"ps -o rss= {processId} | awk '{{printf \" % .0f\\n\", $1 / 1024}}'".RunCommand()?.TrimEnd(), out temp)) processRamUsage = temp;
                //processRamUsage = long.Parse($"ps -o rss= {processId} | awk '{{printf \" % .0f\\n\", $1 / 1024}}'".RunCommand().TrimEnd());
            }
            else
            {
                // CPU Name
                cpu = "wmic cpu get name"
                    .RunCommand()
                    ?.Trim()
                    ?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)?.ElementAtOrDefault(1);

                // Total RAM & total RAM usage
                var output = "wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value"
                    .RunCommand()
                    ?.Trim()
                    ?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

                if (output != null && output.Length > 0)
                {
                    long freeRam = 0;
                    var split = output[0].Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length > 1 && long.TryParse(split[1], out temp))
                    {
                        freeRam = temp / 1024;
                    }

                    split = output[1].Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length > 1 && long.TryParse(split[1], out temp))
                    {
                        totalRam = temp / 1024;
                    }

                    if (totalRam != null && freeRam != 0)
                    {
                        totalRamUsage = totalRam - freeRam;
                    }
                }

                // Process RAM usage
                processRamUsage = Process.GetCurrentProcess().NonpagedSystemMemorySize64 / 1024;
            }

            int totalUsers = 0;
            foreach (var guild in Context.Client.Guilds)
            {
                totalUsers += guild.MemberCount;
            }
            string version = $"v{Constants.Version}";
            if (FergunClient.IsDebugMode)
            {
                version += "-dev";
            }
            var elapsed = DateTime.UtcNow - FergunClient.Uptime; //process.StartTime.ToUniversalTime();

            var builder = new EmbedBuilder()
                .WithTitle("Fergun Stats")

                .AddField(Locate("OperatingSystem"), os, true)
                .AddField("\u200b", "\u200b", true)
                .AddField("CPU", cpu ?? "?", true)

                .AddField(Locate("CPUUsage"), cpuUsage + "%", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("RAMUsage"),
                $"{(processRamUsage == null || totalRam == null ? "?MB" : $"{processRamUsage}MB ({Math.Round((double)processRamUsage.Value / totalRam.Value * 100, 2)}%)")} " +
                $"/ {(totalRamUsage == null || totalRam == null ? "?MB" : $"{totalRamUsage}MB ({Math.Round((double)totalRamUsage.Value / totalRam.Value * 100, 2)}%)")} " +
                $"/ {totalRam?.ToString() ?? "?"}MB", true)

                .AddField(Locate("Library"), $"Discord.Net\nv{DiscordConfig.Version}", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("BotVersion"), version, true)

                .AddField(Locate("TotalServers"), Context.Client.Guilds.Count, true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("TotalUsers"), totalUsers, true)

                .AddField("Uptime", elapsed.ToShortForm2(), true)

                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("BotOwner"), owner.Mention, true);

            if (!FergunClient.IsDebugMode)
            {
                builder.AddField("Links",
                    string.Format(Locate("Links"),
                    FergunClient.InviteLink,
                    FergunClient.DblBotPage,
                    $"{FergunClient.DblBotPage}/vote",
                    FergunConfig.SupportServer));
            }
            builder.WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("support")]
        [Summary("supportSummary")]
        [Alias("bugs", "contact", "report")]
        public async Task Support()
        {
            var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
            await SendEmbedAsync(string.Format(Locate("ContactInfo"), FergunConfig.SupportServer, owner.ToString()));
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
        [Example("computers")]
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

            TriviaPlayer currentPlayer = FergunClient.Database.Find<TriviaPlayer>("TriviaStats", x => x.ID == Context.User.Id)
                ?? new TriviaPlayer(Context.User.Id);

            if (category == "leaderboard" || category == "ranks")
            {
                if (Context.IsPrivate)
                {
                    return FergunResult.FromError(Locate("NotSupportedInDM"));
                }
                string userList = "";
                string pointsList = "";
                int totalUsers = 0;
                var playerList = FergunClient.Database.LoadRecords<TriviaPlayer>("TriviaStats").OrderByDescending(x => x.Points);
                foreach (var player in playerList.Take(15))
                {
                    var user = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, player.ID);
                    if (user != null)
                    {
                        totalUsers++;
                        userList += $"{user}\n";
                        pointsList += $"{player.Points}\n";
                    }
                }

                builder.WithTitle(Locate("TriviaLeaderboard"))
                    .AddField(Locate("User"), totalUsers == 0 ? "?" : userList, true)
                    .AddField(Locate("Points"), totalUsers == 0 ? "?" : pointsList, true)
                    .WithColor(FergunConfig.EmbedColor);
                await ReplyAsync(embed: builder.Build());

                return FergunResult.FromSuccess();
            }

            int index = 0;
            if (category != null)
            {
                index = Array.FindIndex(categories, x => x == category);
                if (index <= -1)
                {
                    index = 0;
                }
            }
            var trivia = TriviaApi.RequestQuestions(1, category == null ? QuestionCategory.Any : (QuestionCategory)(index + 9), encoding: ResponseEncoding.url3986);

            if (trivia.ResponseCode != 0)
            {
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
                optionsText += $"{i + 1}. {Uri.UnescapeDataString(options[i])}\n";
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
                    builder.Description = $"{Locate("Lost1Point")}\n{Locate("TheAnswerIs")} {Format.Code(Uri.UnescapeDataString(question.CorrectAnswer))}";
                }

                builder.WithFooter($"{Locate("Points")}: {currentPlayer.Points}");
                builder.WithColor(FergunConfig.EmbedColor);
                FergunClient.Database.UpdateRecord("TriviaStats", currentPlayer);
                await message.ModifyAsync(x => x.Embed = builder.Build());
            }

            int time = (Array.IndexOf(difficulties, question.Difficulty) * 5) + (question.Type == "multiple" ? 10 : 5);

            builder.WithAuthor(Context.User)
               .WithTitle("Trivia")
               .AddField(Locate("Category"), Uri.UnescapeDataString(question.Category), true)
               .AddField(Locate("Type"), Uri.UnescapeDataString(question.Type), true)
               .AddField(Locate("Difficulty"), Uri.UnescapeDataString(question.Difficulty), true)
               .AddField(Locate("Question"), Uri.UnescapeDataString(question.Question))
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
                // i have to pass the option to a temp var because passing the value directly throws ArgumentOutOfRangeException for some reason
                string option = options[i];
                data.AddCallBack(new Emoji($"{i + 1}\ufe0f\u20e3"), async (_, _1) =>
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
    }
}