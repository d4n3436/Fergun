using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs.OpenTriviaDB;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Services;
using Fergun.Utils;

namespace Fergun.Modules
{
    [Order(5)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class Other : FergunBase
    {
        private static readonly string[] _triviaCategories = Enum.GetNames(typeof(QuestionCategory)).Select(x => x.ToLowerInvariant()).Skip(1).ToArray();
        private static readonly string[] _triviaDifficulties = Enum.GetNames(typeof(QuestionDifficulty)).Select(x => x.ToLowerInvariant()).ToArray();
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = Constants.HttpClientTimeout };
        private static CommandService _cmdService;
        private static LogService _logService;
        private static MessageCacheService _messageCache;

        public Other(CommandService commands, LogService logService, MessageCacheService messageCache)
        {
            _cmdService ??= commands;
            _logService ??= logService;
            _messageCache ??= messageCache;
        }

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
                .WithColor(FergunClient.Config.EmbedColor);

            var split = Locate($"Changelog{version}").SplitBySeparatorWithLimit('\n', EmbedFieldBuilder.MaxFieldValueLength).ToList();
            for (int i = 0; i < split.Count; i++)
            {
                builder.AddField(i == 0 ? $"v{version}" : "\u200b", split[i]);
            }

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("code", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("codeSummary")]
        [Alias("source")]
        [Example("img")]
        public async Task<RuntimeResult> Code([Remainder, Summary("codeParam1")] string commandName)
        {
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
            if (command == null)
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }

            string link = $"{Constants.GitHubRepository}/raw/master/src/Modules/{command.Module.Name}.cs";
            string code;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Code: Downloading code from: {link}"));
            try
            {
                code = await _httpClient.GetStringAsync(new Uri(link));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error downloading the code for module: {command.Module.Name}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error downloading the code for module: {command.Module.Name}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            // Not the best way to get the line number of a method, but it just works ¯\_(ツ)_/¯
            bool found = false;
            var lines = code.Replace("\r", "", StringComparison.OrdinalIgnoreCase).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"[Command(\"{command.Name}\"", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                }
                if (found && lines[i].Contains("public async Task", StringComparison.OrdinalIgnoreCase))
                {
                    await ReplyAsync($"{Constants.GitHubRepository}/blob/master/src/Modules/{command.Module.Name}.cs#L{i + 1}");
                    return FergunResult.FromSuccess();
                }
            }

            return FergunResult.FromError(Locate("CouldNotFindLine"));
        }

        [Command("cmdstats", RunMode = RunMode.Async)]
        [Summary("cmdstatsSummary")]
        [Alias("commandstats")]
        public async Task<RuntimeResult> CmdStats()
        {
            var stats = DatabaseConfig.CommandStats.OrderByDescending(x => x.Value);
            int i = 1;
            string current = "";
            var pages = new List<EmbedBuilder>();

            foreach (var pair in stats)
            {
                string command = $"{i}. {Format.Code(pair.Key)}: {pair.Value}\n";
                if (command.Length + current.Length > EmbedFieldBuilder.MaxFieldValueLength / 2)
                {
                    pages.Add(new EmbedBuilder { Description = current });
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
                pages.Add(new EmbedBuilder { Description = current });
            }
            if (pages.Count == 0)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            string creationDate = Context.Client.CurrentUser.CreatedAt.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

            var pager = new PaginatedMessage
            {
                Title = string.Format(Locate("CommandStatsInfo"), creationDate),
                Pages = pages,
                Color = new Color(FergunClient.Config.EmbedColor),
                Options = new PaginatorAppearanceOptions
                {
                    FooterFormat = Locate("PaginatorFooter"),
                    Timeout = TimeSpan.FromMinutes(10),
                    First = Emote.Parse("<:first:848439761814159381>"),
                    Back = Emote.Parse("<:previous:848439776578502676>"),
                    Next = Emote.Parse("<:next:848439790558248980>"),
                    Last = Emote.Parse("<:last:848439802718322698>"),
                    Stop = Emote.Parse("<:trash:848439812082892820>")
                }
            };

            await PagedReplyAsync(pager, ReactionList.Default, notCommandUserText: Locate("CannotUseThisInteraction"));
            return FergunResult.FromSuccess();
        }

        [Command("cringe")]
        [Summary("cringeSummary")]
        public async Task Cringe()
        {
            await ReplyAsync("https://cdn.discordapp.com/attachments/838832564583661638/838834775417421874/cringe.mp4");
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

            string complete = command.Module.Group == null ? command.Name : $"{command.Module.Group} {command.Name}";
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            if (guild.DisabledCommands.Contains(complete))
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyDisabled"), Format.Code(complete)));
            }

            guild.DisabledCommands.Add(complete);
            FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Disable: Disabled command \"{complete}\" in server {Context.Guild.Id}."));

            await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandDisabled"), Format.Code(complete)));

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

            string complete = command.Module.Group == null ? command.Name : $"{command.Module.Group} {command.Name}";
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            if (guild.DisabledCommands.Contains(complete))
            {
                guild.DisabledCommands.Remove(complete);
                FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Enable: Enabled command \"{complete}\" in server {Context.Guild.Id}."));

                await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandEnabled"), Format.Code(complete)));
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyEnabled"), Format.Code(complete)));
            }

            return FergunResult.FromSuccess();
        }

        [Command("inspirobot")]
        [Summary("inspirobotSummary")]
        public async Task InspiroBot()
        {
            string img = await _httpClient.GetStringAsync("https://inspirobot.me/api?generate=true");
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Inspirobot: Generated url: {img}"));

            var builder = new EmbedBuilder()
                .WithTitle("InspiroBot")
                .WithImageUrl(img)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("invite")]
        [Summary("inviteSummary")]
        public async Task<RuntimeResult> Invite()
        {
            if (FergunClient.IsDebugMode)
            {
                return FergunResult.FromError("No");
            }

            await SendEmbedAsync(Format.Url(Locate("InviteLink"), FergunClient.InviteLink));
            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [Command("language", RunMode = RunMode.Async)]
        [Summary("languageSummary")]
        [Alias("lang")]
        public async Task<RuntimeResult> Language()
        {
            if (FergunClient.Languages.Count <= 1)
            {
                return FergunResult.FromError(Locate("NoAvailableLanguages"));
            }

            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);

            string languages = "";
            var component = new ComponentBuilder();
            int i = 0;

            foreach (var language in FergunClient.Languages)
            {
                if (guild.Language == language.Key)
                    continue;

                languages += $"{i + 1}. {Format.Bold(language.Value.EnglishName)} ({language.Value.NativeName})\n";
                component.WithButton($"{i + 1}".ToString(), language.Key, row: i / 5);

                i++;
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("LanguageSelection"))
                .WithDescription($"{Locate("LanguagePrompt")}\n\n{languages}")
                .WithColor(FergunClient.Config.EmbedColor);

            var message = await Context.Channel.SendMessageAsync(embed: builder.Build(), component: component.Build());

            var interaction = await NextInteractionAsync(
                x => x is SocketMessageComponent messageComponent &&
                     messageComponent.User?.Id == Context.User.Id &&
                     messageComponent.Message.Id == message.Id, TimeSpan.FromMinutes(1));

            if (interaction == null)
            {
                var warningBuilder = new EmbedBuilder()
                    .WithColor(FergunClient.Config.EmbedColor)
                    .WithDescription($"\u26a0 {Locate("ReplyTimeout")}");

                await message.ModifyOrResendAsync(embed: warningBuilder.Build());

                return FergunResult.FromError(Locate("ReplyTimeout"), true);
            }

            string newLanguage = ((SocketMessageComponent)interaction).Data.CustomId;

            guild.Language = newLanguage;
            FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);

            builder.WithTitle(Locate("LanguageSelection"))
                .WithDescription($"✅ {Locate("NewLanguage")}");

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Language: Updated language to: \"{newLanguage}\" in {Context.Guild.Name}"));

            await interaction.RespondAsync(embed: builder.Build(), type: InteractionResponseType.UpdateMessage, component: new ComponentBuilder().Build());

            return FergunResult.FromSuccess();
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
                return FergunResult.FromError(string.Format(Locate("PrefixTooLarge"), Constants.MaxPrefixLength));
            }

            // null prefix = use the global prefix
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            guild.Prefix = newPrefix == DatabaseConfig.GlobalPrefix ? null : newPrefix;

            FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);
            GuildUtils.PrefixCache[Context.Guild.Id] = newPrefix;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Prefix: Updated prefix to: \"{newPrefix}\" in {Context.Guild.Name}"));

            await SendEmbedAsync(string.Format(Locate("NewPrefix"), newPrefix));
            return FergunResult.FromSuccess();
        }

        [AlwaysEnabled]
        [LongRunning]
        [Command("privacy", RunMode = RunMode.Async), Ratelimit(1, 1, Measure.Minutes)]
        [Summary("privacySummary")]
        public async Task<RuntimeResult> Privacy()
        {
            string listToShow = "";
            string[] configList = Locate("PrivacyConfigList").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < configList.Length; i++)
            {
                listToShow += $"**{i + 1}.** {configList[i]}\n";
            }

            var userConfig = GuildUtils.UserConfigCache.GetValueOrDefault(Context.User.Id, new UserConfig(Context.User.Id));

            string valueList = Locate(userConfig!.IsOptedOutSnipe);

            IUserMessage message = null;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("PrivacyPolicy"))
                .AddField(Locate("WhatDataWeCollect"), Locate("WhatDataWeCollectList"))
                .AddField(Locate("WhenWeCollectData"), Locate("WhenWeCollectDataList"))
                .AddField(Locate("PrivacyConfig"), Locate("PrivacyConfigInfo"))
                .AddField(Locate("Option"), listToShow, true)
                .AddField(Locate("Value"), valueList, true)
                .WithColor(FergunClient.Config.EmbedColor);

            var data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(5))
                .AddCallBack(new Emoji("1️⃣"), async (_, reaction) =>
                {
                    userConfig.IsOptedOutSnipe = !userConfig.IsOptedOutSnipe;
                    await HandleReactionAsync(reaction);
                })
                .AddCallBack(new Emoji("❌"), async (_, reaction) =>
                {
                    await message.TryDeleteAsync();
                });

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();

            async Task HandleReactionAsync(SocketReaction reaction)
            {
                FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollection, userConfig);
                GuildUtils.UserConfigCache[Context.User.Id] = userConfig;
                valueList = Locate(userConfig.IsOptedOutSnipe);

                builder.Fields[4] = new EmbedFieldBuilder { Name = Locate("Value"), Value = valueList, IsInline = true };
                _ = message!.RemoveReactionAsync(reaction.Emote, reaction.UserId);
                await message.ModifyAsync(x => x.Embed = builder.Build());
            }
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
                msg = await Context.Channel.GetMessageAsync(_messageCache, messageId.Value);
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
                    if (!Emote.TryParse(reaction, out var tempEmote))
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
            long processRamUsage = 0;
            long? totalRam = null;
            string os = RuntimeInformation.OSDescription;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // CPU Name
                if (File.Exists("/proc/cpuinfo"))
                {
                    var cpuinfo = File.ReadAllLines("/proc/cpuinfo");
                    cpu = cpuinfo.ElementAtOrDefault(4)?.Split(':').ElementAtOrDefault(1);
                }

                // OS Name
                if (File.Exists("/etc/lsb-release"))
                {
                    var distroInfo = File.ReadAllLines("/etc/lsb-release");
                    os = distroInfo.ElementAtOrDefault(3)?.Split('=').ElementAtOrDefault(1)?.Trim('\"');
                }

                // Total RAM & total RAM usage
                var output = CommandUtils.RunCommand("free -m")?.Split(Environment.NewLine);
                var memory = output?.ElementAtOrDefault(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (long.TryParse(memory?.ElementAtOrDefault(1), out temp)) totalRam = temp;
                if (long.TryParse(memory?.ElementAtOrDefault(2), out temp)) totalRamUsage = temp;

                // Process RAM usage
                processRamUsage = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // CPU Name
                cpu = CommandUtils.RunCommand("wmic cpu get name")
                    ?.Trim()
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .ElementAtOrDefault(1);

                // Total RAM & total RAM usage
                var output = CommandUtils.RunCommand("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value")
                    ?.Trim()
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

                if (output?.Length > 1)
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
                processRamUsage = Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024;
            }
            else
            {
                // TODO: Get system info from the remaining platforms
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
            var elapsed = DateTimeOffset.UtcNow - FergunClient.Uptime;

            var builder = new EmbedBuilder()
                .WithTitle("Fergun Stats")

                .AddField(Locate("OperatingSystem"), os, true)
                .AddField("\u200b", "\u200b", true)
                .AddField("CPU", cpu ?? "?", true)

                .AddField(Locate("CPUUsage"), cpuUsage + "%", true)
                .AddField("\u200b", "\u200b", true)
                .AddField(Locate("RAMUsage"),
                    $"{processRamUsage}MB ({(totalRam == null ? 0 : Math.Round((double)processRamUsage / totalRam.Value * 100, 2))}%) " +
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
                .AddField(Locate("BotOwner"), owner, true);

            MessageComponent component = null;
            if (!FergunClient.IsDebugMode)
            {
                component = CommandUtils.BuildLinks(Context.Channel);
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build(), component: component);
        }

        [Command("support")]
        [Summary("supportSummary")]
        [Alias("bugs", "contact", "report")]
        public async Task Support()
        {
            var owner = (await Context.Client.GetApplicationInfoAsync()).Owner;
            if (string.IsNullOrEmpty(FergunClient.Config.SupportServer))
            {
                await SendEmbedAsync(string.Format(Locate("ContactInfoNoServer"), owner));
            }
            else
            {
                await SendEmbedAsync(string.Format(Locate("ContactInfo"), FergunClient.Config.SupportServer, owner));
            }
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
                Color = new Color(FergunClient.Config.EmbedColor)
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
                Color = new Color(FergunClient.Config.EmbedColor)
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
                    .WithDescription(string.Join("\n", _triviaCategories))
                    .WithColor(FergunClient.Config.EmbedColor);

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

                foreach (var user in GuildUtils.UserConfigCache.Values)
                {
                    GuildUtils.UserConfigCache[user.Id].TriviaPoints = 0;
                    user.TriviaPoints = 0;
                    FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollection, user);
                }
                return FergunResult.FromSuccess();
            }

            var userConfig = GuildUtils.UserConfigCache.GetValueOrDefault(Context.User.Id, new UserConfig(Context.User.Id));

            if (category == "leaderboard" || category == "ranks")
            {
                if (Context.IsPrivate)
                {
                    return FergunResult.FromError(Locate("NotSupportedInDM"));
                }
                string userList = "";
                string pointsList = "";
                int totalUsers = 0;
                foreach (var user in GuildUtils.UserConfigCache.Values.Take(15))
                {
                    var guildUser = await Context.Client.Rest.GetGuildUserAsync(Context.Guild.Id, user.Id);
                    if (guildUser == null) continue;

                    totalUsers++;
                    userList += $"{guildUser}\n";
                    pointsList += $"{user.TriviaPoints}\n";
                }

                builder.WithTitle(Locate("TriviaLeaderboard"))
                    .AddField(Locate("User"), totalUsers == 0 ? "?" : userList, true)
                    .AddField(Locate("Points"), totalUsers == 0 ? "?" : pointsList, true)
                    .WithColor(FergunClient.Config.EmbedColor);
                await ReplyAsync(embed: builder.Build());

                return FergunResult.FromSuccess();
            }

            int index = 0;
            if (category != null)
            {
                index = Array.FindIndex(_triviaCategories, x => x == category);
                if (index <= -1)
                {
                    index = 0;
                }
            }
            var trivia = await TriviaApi.RequestQuestionsAsync(1, category == null ? QuestionCategory.Any : (QuestionCategory)(index + 9), encoding: ResponseEncoding.url3986);

            if (trivia.ResponseCode != 0)
            {
                return FergunResult.FromError(trivia.ResponseCode == 4 ? Locate("AllQuestionsAnswered") : $"{Locate("TriviaError")} {trivia.ResponseCode}: {(ResponseCode)trivia.ResponseCode}");
            }
            var question = trivia.Questions[0];

            var options = new List<string>(question.IncorrectAnswers)
            {
                question.CorrectAnswer
            };

            options.Shuffle();

            IUserMessage message = null;
            bool hasReacted = false;
            var callbacks = new List<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)>();
            string optionsText = "";
            int i = 0;
            foreach (string option in options)
            {
                optionsText += $"{i + 1}. {Uri.UnescapeDataString(options[i])}\n";
                callbacks.Add((new Emoji($"{i + 1}\ufe0f\u20e3"), async (context, reaction) => await HandleTriviaReactionAsync(option)));
                i++;
            }

            int time = (Array.IndexOf(_triviaDifficulties, question.Difficulty) * 5) + (question.Type == "multiple" ? 10 : 5);

            builder.WithAuthor(Context.User)
                .WithTitle("Trivia")
                .AddField(Locate("Category"), Uri.UnescapeDataString(question.Category), true)
                .AddField(Locate("Type"), Uri.UnescapeDataString(question.Type), true)
                .AddField(Locate("Difficulty"), Uri.UnescapeDataString(question.Difficulty), true)
                .AddField(Locate("Question"), Uri.UnescapeDataString(question.Question))
                .AddField(Locate("Options"), optionsText)
                .WithFooter(string.Format(Locate("TimeLeft"), time))
                .WithColor(FergunClient.Config.EmbedColor);

            var data = new ReactionCallbackData(null, builder.Build(), true, true, TimeSpan.FromSeconds(time),
                async context => await HandleTriviaReactionAsync(null)).AddCallbacks(callbacks);

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();

            async Task HandleTriviaReactionAsync(string option)
            {
                if (hasReacted) return;
                hasReacted = true;

                builder = new EmbedBuilder();
                if (option == null)
                {
                    userConfig!.TriviaPoints--;
                    builder.Title = $"❌ {Locate("TimesUp")}";
                    builder.Description = Locate("Lost1Point");
                }
                else if (option == question.CorrectAnswer)
                {
                    userConfig!.TriviaPoints++;
                    builder.Title = $"✅ {Locate("CorrectAnswer")}";
                    builder.Description = Locate("Won1Point");
                }
                else
                {
                    userConfig!.TriviaPoints--;
                    builder.Title = $"❌ {Locate("Incorrect")}";
                    builder.Description = $"{Locate("Lost1Point")}\n{Locate("TheAnswerIs")} {Format.Code(Uri.UnescapeDataString(question.CorrectAnswer))}";
                }

                builder.WithFooter($"{Locate("Points")}: {userConfig.TriviaPoints}")
                    .WithColor(FergunClient.Config.EmbedColor);

                FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollection, userConfig);
                GuildUtils.UserConfigCache[Context.User.Id] = userConfig;
                await message!.ModifyAsync(x => x.Embed = builder.Build());
            }
        }

        [Command("uptime")]
        [Summary("uptimeSummary")]
        public async Task Uptime()
        {
            var elapsed = DateTimeOffset.UtcNow - FergunClient.Uptime;

            var builder = new EmbedBuilder
            {
                Title = "Bot uptime",
                Description = elapsed.ToShortForm2(),
                Color = new Color(FergunClient.Config.EmbedColor)
            };

            await ReplyAsync(embed: builder.Build());
        }

        [Command("vote")]
        [Summary("voteSummary")]
        public async Task<RuntimeResult> Vote()
        {
            if (FergunClient.IsDebugMode)
            {
                return FergunResult.FromError("No");
            }
            if (FergunClient.DblBotPage == null)
            {
                return FergunResult.FromError(Locate("NowhereToVote"));
            }

            var builder = new EmbedBuilder
            {
                Description = string.Format(Locate("Vote"), $"{FergunClient.DblBotPage}/vote"),
                Color = new Color(FergunClient.Config.EmbedColor)
            };

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }
    }
}