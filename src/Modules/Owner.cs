using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Attributes;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Services;
using Fergun.Utils;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Fergun.Modules
{
    [AlwaysEnabled]
    [RequireBotPermission(Constants.MinimunRequiredPermissions)]
    [RequireOwner(ErrorMessage = "BotOwnerOnly")]
    public class Owner : FergunBase
    {
        private static IEnumerable<Assembly> _scriptAssemblies;
        private static IEnumerable<string> _scriptNamespaces;
        private static ScriptOptions _scriptOptions;
        private static CommandService _cmdService;
        private static LogService _logService;
        private readonly MusicService _musicService;

        public Owner(CommandService commands, LogService logService, MusicService musicService)
        {
            _cmdService ??= commands;
            _logService ??= logService;
            _musicService ??= musicService;
        }

        [LongRunning]
        [Command("bash", RunMode = RunMode.Async)]
        [Summary("bashSummary")]
        [Alias("sh", "cmd")]
        [Example("ping discord.com -c 4")]
        public async Task Bash([Remainder] string command)
        {
            string result = command.RunCommand();
            if (string.IsNullOrWhiteSpace(result))
            {
                await SendEmbedAsync("No output.");
            }
            else
            {
                await ReplyAsync(Format.Code(result.Truncate(DiscordConfig.MaxMessageSize - 10), "md"));
            }
        }

        [Command("blacklist", RunMode = RunMode.Async)]
        [Summary("blacklistSummary")]
        [Example("666963870385923507 bot abuse")]
        public async Task Blacklist([Summary("blacklistParam1")] ulong userId,
            [Remainder, Summary("blacklistParam2")] string reason = null)
        {
            var userConfig = FergunClient.Database.FindDocument<UserConfig>(Constants.UserConfigCollecion, x => x.Id == userId) ?? new UserConfig(userId);
            if (userConfig.IsBlacklisted)
            {
                userConfig.IsBlacklisted = false;
                userConfig.BlacklistReason = null;
                FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollecion, userConfig);

                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Removed user {userId} from the blacklist."));
                await SendEmbedAsync(string.Format(Locate("UserBlacklistRemoved"), userId));
            }
            else
            {
                userConfig.IsBlacklisted = true;
                userConfig.BlacklistReason = reason;
                FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollecion, userConfig);

                if (reason == null)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Added user {userId} to the blacklist."));
                    await SendEmbedAsync(string.Format(Locate("UserBlacklisted"), userId));
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Added user {userId} to the blacklist with reason: {reason}"));
                    await SendEmbedAsync(string.Format(Locate("UserBlacklistedWithReason"), userId, reason));
                }
            }
        }

        [Command("blacklistserver", RunMode = RunMode.Async)]
        [Summary("blacklistserverSummary")]
        [Example("685963870363423500 bot farm")]
        public async Task Blacklistserver([Summary("blacklistserverParam1")] ulong serverId,
            [Remainder, Summary("blacklistserverParam2")] string reason = null)
        {
            var serverConfig = FergunClient.Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, x => x.Id == serverId) ?? new GuildConfig(serverId);
            if (serverConfig.IsBlacklisted)
            {
                serverConfig.IsBlacklisted = false;
                serverConfig.BlacklistReason = null;
                FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, serverConfig);

                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Removed server {serverId} from the blacklist."));
                await SendEmbedAsync(string.Format(Locate("UserBlacklistRemoved"), serverId));
            }
            else
            {
                serverConfig.IsBlacklisted = true;
                serverConfig.BlacklistReason = reason;
                FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, serverConfig);

                if (reason == null)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Added server {serverId} to the blacklist."));
                    await SendEmbedAsync(string.Format(Locate("ServerBlacklisted"), serverId));
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"Added server {serverId} to the blacklist with reason: {reason}"));
                    await SendEmbedAsync(string.Format(Locate("ServerBlacklistedWithReason"), serverId, reason));
                }

                var server = Context.Client.Guilds.FirstOrDefault(x => x.Id == serverId);
                if (server != null)
                {
                    await server.LeaveAsync();
                }
            }
        }

        [Command("botgame")]
        [Summary("botgameSummary")]
        [Example("f!help | fergun.com")]
        public async Task Botgame([Remainder, Summary("botgameParam1")] string text)
        {
            await Context.Client.SetGameAsync(text);
            if (Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await Context.Message.AddReactionAsync(new Emoji("\u2705"));
            }
        }

        [Command("botstatus")]
        [Summary("botstatusSummary")]
        [Example("1")]
        public async Task Botstatus([Summary("botstatusParam1")] uint status)
        {
            if (status <= 5)
            {
                await Context.Client.SetStatusAsync((UserStatus)status);
                if (Context.Guild.CurrentUser.GuildPermissions.AddReactions)
                {
                    await Context.Message.AddReactionAsync(new Emoji("\u2705"));
                }
            }
        }

        [Command("eval", RunMode = RunMode.Async)]
        [Summary("evalSummary")]
        [Example("return Context.Client.Guilds.Count();")]
        public async Task Eval([Remainder, Summary("evalParam1")] string code)
        {
            Stopwatch sw = new Stopwatch();
            code = code.Trim('`'); //Remove code block tags
            bool silent = false;
            if (code.EndsWith("-s", StringComparison.OrdinalIgnoreCase))
            {
                silent = true;
                code = code.Substring(0, code.Length - 2);
            }
            if (!silent)
            {
                await Context.Channel.TriggerTypingAsync();
                sw.Start();
            }

            _scriptAssemblies ??= AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location));

            _scriptNamespaces ??= Assembly.GetEntryAssembly()?.GetTypes()?
                .Where(x => !string.IsNullOrWhiteSpace(x.Namespace))?.Select(x => x.Namespace)?.Distinct()
                .Append("System")
                .Append("System.IO")
                .Append("System.Math")
                .Append("System.Diagnostics")
                .Append("System.Linq")
                .Append("System.Collections.Generic")
                .Append("Discord")
                .Append("Discord.WebSocket");

            _scriptOptions ??= ScriptOptions.Default
                .AddReferences(_scriptAssemblies)
                .AddImports(_scriptNamespaces);

            var script = CSharpScript.Create(code, _scriptOptions, typeof(EvaluationEnvironment));
            object returnValue;
            string returnType = null;
            try
            {
                var globals = new EvaluationEnvironment(Context);
                var scriptState = await script.RunAsync(globals);
                returnValue = scriptState?.ReturnValue;
                returnType = returnValue?.GetType()?.Name ?? "none";
            }
            catch (CompilationErrorException e)
            {
                returnValue = e.Message;
                returnType = e.GetType()?.Name ?? "none";
            }
            if (silent)
            {
                return;
            }
            sw.Stop();

            if (returnValue == null)
            {
                await SendEmbedAsync(Locate("EvalNoReturnValue"));
                return;
            }

            string value = returnValue.ToString();
            if (value.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                var pages = value
                    .SplitBySeparatorWithLimit('\n', EmbedBuilder.MaxDescriptionLength - 10)
                    .Select(x => new EmbedBuilder { Description = Format.Code(x.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase), "md") });

                var pager = new PaginatedMessage()
                {
                    Author = new EmbedAuthorBuilder()
                    {
                        Name = Context.User.ToString(),
                        IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                    },
                    Title = Locate("EvalResults"),
                    Pages = pages,
                    Fields = new List<EmbedFieldBuilder>()
                    {
                        new EmbedFieldBuilder()
                        .WithName(Locate("Type"))
                        .WithValue(Format.Code(returnType, "md"))
                    },
                    Color = new Color(FergunClient.Config.EmbedColor),
                    Options = new PaginatorAppearanceOptions()
                    {
                        FooterFormat = $"{string.Format(Locate("EvalFooter"), sw.ElapsedMilliseconds)} - {Locate("PaginatorFooter")}",
                        Timeout = TimeSpan.FromMinutes(10),
                        ActionOnTimeout = ActionOnTimeout.DeleteReactions
                    }
                };

                await PagedReplyAsync(pager, ReactionList.Default);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithTitle(Locate("EvalResults"))
                    .AddField(Locate("Input"), Format.Code(code.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                    .AddField(Locate("Output"), Format.Code(value.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase), "md"))
                    .AddField(Locate("Type"), Format.Code(returnType, "md"))
                    .WithFooter(string.Format(Locate("EvalFooter"), sw.ElapsedMilliseconds))
                    .WithColor(FergunClient.Config.EmbedColor);

                await ReplyAsync(embed: builder.Build());
            }
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("forceprefix")]
        [Summary("forceprefixSummary")]
        [Example("!")]
        public async Task<RuntimeResult> Forceprefix([Summary("prefixParam1")] string newPrefix)
        {
            if (newPrefix == GetPrefix())
            {
                return FergunResult.FromError(Locate("PrefixSameCurrentTarget"));
            }

            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            if (newPrefix == DatabaseConfig.GlobalPrefix)
            {
                guild.Prefix = null; //Default prefix
            }
            else
            {
                guild.Prefix = newPrefix;
            }

            FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);
            GuildUtils.PrefixCache[Context.Guild.Id] = newPrefix;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Forceprefix: Force updated prefix to: \"{newPrefix}\" in {Context.Guild.Name}"));

            await SendEmbedAsync(string.Format(Locate("NewPrefix"), newPrefix));
            return FergunResult.FromSuccess();
        }

        [Command("globaldisable", RunMode = RunMode.Async)]
        [Summary("globaldisableSummary")]
        [Example("img")]
        public async Task<RuntimeResult> Globaldisable([Summary("globaldisableParam1")] string commandName,
            [Remainder, Summary("globaldisableParam2")] string reason = null)
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

            var disabledCommands = DatabaseConfig.GloballyDisabledCommands;
            if (disabledCommands.ContainsKey(command.Name))
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyDisabledGlobally"), Format.Code(command.Name)));
            }

            disabledCommands.Add(command.Name, reason);
            DatabaseConfig.Update(x => x.GloballyDisabledCommands = disabledCommands);
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Globaldisable: Disabled command {command.Name} in all servers."));

            await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandDisabledGlobally"), Format.Code(command.Name)));

            return FergunResult.FromSuccess();
        }

        [Command("globalenable", RunMode = RunMode.Async)]
        [Summary("globalenableSummary")]
        [Example("img")]
        public async Task<RuntimeResult> Globalenable([Summary("globalenableParam1")] string commandName)
        {
            var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
            if (command != null)
            {
                if (command.Attributes.Concat(command.Module.Attributes).Any(x => x is AlwaysEnabledAttribute))
                {
                    return FergunResult.FromError(string.Format(Locate("AlreadyEnabledGlobally"), Format.Code(command.Name)));
                }
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
            }

            var disabledCommands = DatabaseConfig.GloballyDisabledCommands;
            if (disabledCommands.ContainsKey(command.Name))
            {
                disabledCommands.Remove(command.Name);
                DatabaseConfig.Update(x => x.GloballyDisabledCommands = disabledCommands);
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Globalenable: Enabled command {command.Name} in all servers."));

                await SendEmbedAsync("\u2705 " + string.Format(Locate("CommandEnabledGlobally"), Format.Code(command.Name)));
            }
            else
            {
                return FergunResult.FromError(string.Format(Locate("AlreadyEnabledGlobally"), Format.Code(command.Name)));
            }
            return FergunResult.FromSuccess();
        }

        [Command("globalprefix")]
        [Summary("globalprefixSummary")]
        [Example("f!")]
        public async Task<RuntimeResult> Globalprefix([Summary("globalprefixParam1")] string newPrefix)
        {
            if (newPrefix == DatabaseConfig.GlobalPrefix)
            {
                return FergunResult.FromError(Locate("PrefixSameCurrentTarget"));
            }
            if (FergunClient.IsDebugMode)
            {
                DatabaseConfig.Update(x => x.DevGlobalPrefix = newPrefix);
            }
            else
            {
                DatabaseConfig.Update(x => x.GlobalPrefix = newPrefix);
            }
            GuildUtils.CachedGlobalPrefix = newPrefix;
            await SendEmbedAsync(string.Format(Locate("NewGlobalPrefix"), newPrefix));

            return FergunResult.FromSuccess();
        }

        [Command("logout")]
        [Summary("logoutSummary")]
        [Alias("die")]
        public async Task<RuntimeResult> Logout()
        {
            await _musicService.ShutdownAllPlayersAsync();

            await ReplyAsync("Bye bye");
            await Context.Client.SetStatusAsync(UserStatus.Invisible);
            //await Context.Client.LogoutAsync();
            await Context.Client.StopAsync();
            Cache.Dispose();
            Environment.Exit(0);

            return FergunResult.FromError("Wait. This line was not supposed to be reached.");
        }

        [Command("restart")]
        [Summary("restartSummary")]
        public async Task<RuntimeResult> Restart()
        {
            await _musicService.ShutdownAllPlayersAsync();

            if (Context.Guild.CurrentUser.GuildPermissions.AddReactions)
            {
                await Context.Message.AddReactionAsync(new Emoji("\u2705"));
            }

            Process.Start(AppDomain.CurrentDomain.FriendlyName);
            await Context.Client.SetStatusAsync(UserStatus.Idle);
            Cache.Dispose();
            Environment.Exit(0);

            return FergunResult.FromError("Wait. This line was not supposed to be reached.");
        }
    }

    public sealed class EvaluationEnvironment
    {
        public SocketCommandContext Context { get; }
        public SocketUserMessage Message => Context.Message;
        public ISocketMessageChannel Channel => Context.Channel;
        public SocketGuild Guild => Context.Guild;
        public SocketUser User => Context.User;
        public DiscordSocketClient Client => Context.Client;

        public EvaluationEnvironment(SocketCommandContext context)
        {
            Context = context;
        }
    }
}