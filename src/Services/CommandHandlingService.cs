using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _client;
        private readonly LogService _logService;
        private readonly CommandService _cmdService;
        private readonly IServiceProvider _services;
        private bool _isValidLogChannel = true;

        private static readonly HashSet<ulong> _ignoredUsers = new HashSet<ulong>();
        private static readonly object _userLock = new object();
        private static readonly object _cmdStatsLock = new object();

        public CommandHandlingService(DiscordSocketClient client, CommandService commands, LogService logService, IServiceProvider services)
        {
            _client = client;
            _cmdService = commands;
            _logService = logService;
            _services = services;

            _client.MessageReceived += HandleCommandAsync;
            _cmdService.CommandExecuted += OnCommandExecutedAsync;
        }

        public async Task InitializeAsync()
        {
            _cmdService.AddTypeReader(typeof(IUser), new Readers.UserTypeReader<IUser>());

            // Here we discover all of the command modules in the entry
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;

            // Ignore empty messages
            if (string.IsNullOrEmpty(message.Content)) return;

            // Ignore messages from bots
            if (message.Author.IsBot) return;

            string prefix = GuildUtils.GetCachedPrefix(message.Channel);
            if (message.Content == prefix) return;

            if (message.Content == _client.CurrentUser.Mention)
            {
                lock (_userLock)
                {
                    if (_ignoredUsers.Contains(message.Author.Id)) return;
                }

                _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromSeconds(Constants.MentionIgnoreTime));
                await SendEmbedAsync(message, string.Format(GuildUtils.Locate("BotMention", message.Channel), prefix));
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"{message.Author} mentioned me."));
                return;
            }

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix or mention
            if (!(message.HasStringPrefix(prefix, ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            // Ignore ignored users
            lock (_userLock)
            {
                if (_ignoredUsers.Contains(message.Author.Id)) return;
            }

            var result = _cmdService.Search(message.Content.Substring(argPos));
            if (!result.IsSuccess) return;

            if (GuildUtils.UserConfigCache.TryGetValue(message.Author.Id, out var userConfig) && userConfig.IsBlacklisted)
            {
                _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromMinutes(Constants.BlacklistIgnoreTime));
                if (userConfig.BlacklistReason == null)
                {
                    await SendEmbedAsync(message, "\u274c " + GuildUtils.Locate("Blacklisted", message.Channel), message.Author.Mention);
                }
                else
                {
                    await SendEmbedAsync(message, "\u274c " + string.Format(GuildUtils.Locate("BlacklistedWithReason", message.Channel), userConfig.BlacklistReason), message.Author.Mention);
                }
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"{message.Author} ({message.Author.Id}) wanted to use the command \"{result.Commands[0].Alias}\" but they are blacklisted."));
                return;
            }

            var disabledCommands = GuildUtils.GetGuildConfig(message.Channel)?.DisabledCommands;
            var disabled = disabledCommands?.FirstOrDefault(x => result.Commands.Any(y =>
                x == (y.Command.Module.Group == null ? y.Command.Name : $"{y.Command.Module.Group} {y.Command.Name}")));

            if (disabled != null)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"User {message.Author} ({message.Author.Id}) tried to use the locally disabled command \"{disabled}\"."));
                await SendEmbedAsync(message, "\u26a0 " + string.Format(GuildUtils.Locate("CommandDisabled", message.Channel), Format.Code(disabled)));
                _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromSeconds(Constants.DefaultIgnoreTime));
            }
            else
            {
                var globalDisabled = DatabaseConfig.GloballyDisabledCommands.FirstOrDefault(x =>
                    result.Commands.Any(y =>
                        x.Key == (y.Command.Module.Group == null ? y.Command.Name : $"{y.Command.Module.Group} {y.Command.Name}")));

                if (globalDisabled.Key != null)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"User {message.Author} ({message.Author.Id}) tried to use the globally disabled command \"{globalDisabled.Key}\"."));
                    await SendEmbedAsync(message, $"\u26a0 {string.Format(GuildUtils.Locate("CommandDisabledGlobally", message.Channel), Format.Code(globalDisabled.Key))}" +
                        $"{(!string.IsNullOrEmpty(globalDisabled.Value) ? $"\n{GuildUtils.Locate("Reason", message.Channel)}: {globalDisabled.Value}" : "")}");
                    _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromSeconds(Constants.DefaultIgnoreTime));
                }
                else
                {
                    // Create a WebSocket-based command context based on the message
                    var context = new SocketCommandContext(_client, message);

                    // Execute the command with the command context we just
                    // created, along with the service provider for precondition checks.
                    await _cmdService.ExecuteAsync(context, argPos, _services);
                }
            }
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> optionalCommand, ICommandContext context, IResult result)
        {
            // We have access to the information of the command executed,
            // the context of the command, and the result returned from the
            // execution in this event.

            // command is unspecified when there was a search failure (command not found)
            if (!optionalCommand.IsSpecified)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"Unknown command: \"{context.Message.Content}\", sent by {context.User} in {context.Display()}"));
                return;
            }

            if (context.Guild != null)
            {
                // Update the last time a command was used in this guild.
                _services.GetService<MessageCacheService>()?.UpdateLastCommandUsageTime(context.Guild.Id);
            }

            var command = optionalCommand.Value;

            if (command.Module.Name != Constants.DevelopmentModuleName)
            {
                // Update the command stats
                lock (_cmdStatsLock)
                {
                    var stats = DatabaseConfig.CommandStats;
                    if (stats.ContainsKey(command.Name))
                    {
                        stats[command.Name]++;
                    }
                    else
                    {
                        stats.Add(command.Name, 1);
                    }
                    DatabaseConfig.Update(x => x.CommandStats = stats);
                }
            }

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess) return;

            var responseMessage = (result as FergunResult)?.ResponseMessage;
            double ignoreTime = Constants.DefaultIgnoreTime;
            switch (result.Error)
            {
                //case CommandError.UnknownCommand:
                //    await SendEmbedAsync(context.Message, string.Format(LocalizationService.Locate("CommandNotFound", context.Message), GetPrefix(context.Channel)));
                //    break;
                case CommandError.BadArgCount:
                case CommandError.ParseFailed:
                    string language = GuildUtils.GetLanguage(context.Channel);
                    string prefix = GuildUtils.GetPrefix(context.Channel);
                    await SendEmbedAsync(context.Message, command.ToHelpEmbed(language, prefix), null, responseMessage);
                    break;

                case CommandError.UnmetPrecondition when command.Module.Name != Constants.DevelopmentModuleName:
                    ChannelPermissions permissions;
                    if (context.Guild == null)
                    {
                        permissions = ChannelPermissions.All(context.Channel);
                    }
                    else
                    {
                        var guildUser = await context.Guild.GetCurrentUserAsync().ConfigureAwait(false);
                        permissions = guildUser.GetPermissions((IGuildChannel)context.Channel);
                    }

                    if (!permissions.Has(Constants.MinimumRequiredPermissions))
                    {
                        var builder = new EmbedBuilder()
                            .WithDescription($"\u26a0 {result.ErrorReason}")
                            .WithColor(FergunClient.Config.EmbedColor);
                        try
                        {
                            await context.User.SendMessageAsync(embed: builder.Build());
                        }
                        catch (HttpException e) when (e.DiscordCode == 50007)
                        {
                            await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Unable to send a DM about the minimum required permissions to the user."));
                        }
                    }
                    else
                    {
                        if (result.ErrorReason.StartsWith("(Cooldown)", StringComparison.OrdinalIgnoreCase))
                        {
                            ignoreTime = Constants.CooldownIgnoreTime;
                        }
                        await SendEmbedAsync(context.Message, $"\u26a0 {GuildUtils.Locate(result.ErrorReason, context.Channel)}", null, responseMessage);
                    }
                    break;

                case CommandError.ObjectNotFound:
                    // reason: The error reason (User not found., Role not found., etc)
                    string reason = result.ErrorReason;
                    // Delete the last char (.)
                    reason = reason.Substring(0, result.ErrorReason.Length - 1);
                    // Convert to title case (User Not Found)
                    reason = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(reason);
                    // Remove spaces (UserNotFound)
                    reason = reason.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
                    // Locate the string to the current language of the guild
                    reason = string.Format(GuildUtils.Locate(reason, context.Channel),
                        Format.Code(context.Client.CurrentUser.ToString()),
                        context.Client.CurrentUser.Mention,
                        Format.Code(context.Client.CurrentUser.Id.ToString()));

                    await SendEmbedAsync(context.Message, $"\u26a0 {reason}", null, responseMessage);
                    break;

                case CommandError.MultipleMatches:
                    string message = string.Format(GuildUtils.Locate("MultipleMatches", context.Channel),
                        Format.Code(context.Client.CurrentUser.ToString()),
                        context.Client.CurrentUser.Mention,
                        Format.Code(context.Client.CurrentUser.Id.ToString()));

                    await SendEmbedAsync(context.Message, $"\u26a0 {message}", null, responseMessage);
                    break;

                case CommandError.Unsuccessful:
                    if (!(result is FergunResult fergunResult) || !fergunResult.IsSilent)
                    {
                        await SendEmbedAsync(context.Message, $"\u26a0 {result.ErrorReason}".Truncate(EmbedBuilder.MaxDescriptionLength), null, responseMessage);
                    }
                    break;

                case CommandError.Exception when result is ExecuteResult execResult:
                    var exception = execResult.Exception;

                    if (exception is HttpException httpException && httpException.HttpCode >= HttpStatusCode.InternalServerError)
                    {
                        await Task.Delay(2000);
                        var builder = new EmbedBuilder()
                            .WithTitle(GuildUtils.Locate("DiscordServerError", context.Channel))
                            .WithDescription($"\u26a0 {GuildUtils.Locate("DiscordServerErrorInfo", context.Channel)}")
                            .AddField(GuildUtils.Locate("ErrorDetails", context.Channel),
                                Format.Code($"Code: {(int)httpException.HttpCode}, Reason: {httpException.Reason}", "md"))
                            .WithColor(FergunClient.Config.EmbedColor);

                        try
                        {
                            await SendEmbedAsync(context.Message, builder.Build());
                        }
                        catch (HttpException) { }
                        break;
                    }

                    var owner = (await context.Client.GetApplicationInfoAsync()).Owner;

                    string errorMessage = Format.Code(exception.Message, "cs");

                    if (context.User.Id != owner.Id)
                    {
                        errorMessage += "\n" + string.Format(GuildUtils.Locate("ErrorHelp", context.Channel), FergunClient.Config.SupportServer, Constants.GitHubRepository);
                    }

                    var builder2 = new EmbedBuilder()
                        .WithTitle($"\u274c {GuildUtils.Locate("FailedExecution", context.Channel)} {Format.Code(command.Name)}")
                        .AddField(GuildUtils.Locate("ErrorType", context.Channel), Format.Code(exception.GetType().Name, "cs"))
                        .AddField(GuildUtils.Locate("ErrorMessage", context.Channel), errorMessage)
                        .WithColor(FergunClient.Config.EmbedColor);

                    await SendEmbedAsync(context.Message, builder2.Build());

                    if (context.User.Id == owner.Id || !_isValidLogChannel || FergunClient.Config.LogChannel == 0) break;
                    // if the user that executed the command isn't the bot owner, send the full stack trace to the errors channel

                    var channel = await context.Client.GetChannelAsync(FergunClient.Config.LogChannel);
                    if (!(channel is IMessageChannel messageChannel))
                    {
                        _isValidLogChannel = false;
                        await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Invalid log channel Id ({FergunClient.Config.LogChannel}). Not possible to send the embed with the error info."));
                        break;
                    }

                    var builder3 = new EmbedBuilder()
                        .WithTitle($"\u274c Failed to execute {Format.Code(command.Name)} in {context.Display()}".Truncate(EmbedBuilder.MaxTitleLength))
                        .AddField(GuildUtils.Locate("ErrorType", messageChannel), Format.Code(exception.GetType().Name, "cs"))
                        .AddField(GuildUtils.Locate("ErrorMessage", messageChannel), Format.Code(exception.ToString().Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "cs"))
                        .AddField("Jump url", context.Message.GetJumpUrl())
                        .AddField("Command", context.Message.Content.Truncate(EmbedFieldBuilder.MaxFieldValueLength))
                        .WithColor(FergunClient.Config.EmbedColor);

                    try
                    {
                        await messageChannel.SendMessageAsync(embed: builder3.Build());
                    }
                    catch (HttpException e)
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error while sending the embed in the log channel", e));
                    }
                    break;
            }

            _ = IgnoreUserAsync(context.User.Id, TimeSpan.FromSeconds(ignoreTime));
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"Failed to execute \"{command.Name}\" for {context.User} in {context.Display()}, with error type: {result.Error} and reason: {result.ErrorReason}"));
        }

        private static async Task IgnoreUserAsync(ulong id, TimeSpan time)
        {
            lock (_userLock)
            {
                _ignoredUsers.Add(id);
            }
            await Task.Delay(time);
            lock (_userLock)
            {
                _ignoredUsers.Remove(id);
            }
        }

        private Task SendEmbedAsync(IUserMessage userMessage, string embedText, string text = null, IUserMessage responseMessage = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(FergunClient.Config.EmbedColor)
                .WithDescription(embedText)
                .Build();

            return SendEmbedAsync(userMessage, embed, text, responseMessage);
        }

        private async Task SendEmbedAsync(IUserMessage userMessage, Embed embed, string text = null, IUserMessage responseMessage = null)
        {
            var messageCache = _services.GetService<MessageCacheService>();
            if (responseMessage == null)
            {
#if DNETLABS
                var component = new ComponentBuilder().Build(); // remove message components
#endif
                var cache = _services.GetService<CommandCacheService>();

                ulong messageId = 0;
                bool found = cache?.TryGetValue(userMessage.Id, out messageId) ?? false;

                var response = found ? (IUserMessage)await userMessage.Channel.GetMessageAsync(messageCache, messageId) : null;

                if (response == null)
                {
#if DNETLABS
                    response = await userMessage.Channel.SendMessageAsync(text, embed: embed, component: component).ConfigureAwait(false);
#else
                    response = await userMessage.Channel.SendMessageAsync(text, embed: embed).ConfigureAwait(false);
#endif 
                }
                else
                {
                    if (_services.GetService<InteractiveService>()?.TryRemoveCallback(messageId, out var callback) ?? false)
                    {
                        callback.Dispose();
                    }

                    await response.ModifyAsync(x =>
                    {
                        x.Content = text;
                        x.Embed = embed;
#if DNETLABS
                        x.Components = component;
#endif
                    });
                }

                if (cache != null && !cache.IsDisabled)
                {
                    cache.Add(userMessage, response);
                }
            }
            else
            {
                await responseMessage.ModifyOrResendAsync(text, embed, cache: messageCache).ConfigureAwait(false);
            }
        }
    }
}