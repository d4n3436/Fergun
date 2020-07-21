using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandCache;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Extensions;
using Fergun.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fergun
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _client;
        private readonly LogService _logService;
        private readonly CommandService _cmdService;
        private readonly IServiceProvider _services;

        private static readonly List<ulong> _ignoredUsers = new List<ulong>();
        private static readonly object _userLock = new object();

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
            // Here we discover all of the command modules in the entry
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.

            _cmdService.AddTypeReader(typeof(IUser), new Readers.UserTypeReader<IUser>());
            _cmdService.AddTypeReader(typeof(IGuildUser), new Readers.UserTypeReader<IGuildUser>());

            await _cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;

            // Ignore messages from bots
            if (message.Author.IsBot) return;

            if (_ignoredUsers.Contains(message.Author.Id)) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            if (message.Channel is SocketGuildChannel guildChannel && !guildChannel.Guild.CurrentUser.GuildPermissions.SendMessages)
            {
                return;
            }

            string prefix = Localizer.GetPrefix(message.Channel);
            if (message.Content == prefix)
            {
                return;
            }

            if (message.Content == _client.CurrentUser.Mention)
            {
                _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromSeconds(1));
                await SendEmbedAsync(message, string.Format(Localizer.Locate("BotMention", message.Channel), prefix), _services);
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"{message.Author} mentioned me."));
                return;
            }

            // Determine if the message is a command based on the prefix or mention
            if (!(message.HasStringPrefix(prefix, ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            var blacklistedUser = FergunClient.Database.Find<BlacklistEntity>("Blacklist", x => x.ID == message.Author.Id);
            if (blacklistedUser != null)
            {
                _ = IgnoreUserAsync(message.Author.Id, TimeSpan.FromMinutes(5));
                if (blacklistedUser.Reason == null)
                {
                    await SendEmbedAsync(message, "\u274c " + Localizer.Locate("Blacklisted", message.Channel), _services, message.Author.Mention);
                }
                else
                {
                    await SendEmbedAsync(message, "\u274c " + string.Format(Localizer.Locate("BlacklistedWithReason", message.Channel), blacklistedUser.Reason), _services, message.Author.Mention);
                }
                await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Blacklist", $"{message.Author} ({message.Author.Id}) wanted to use a command but they are blacklisted."));
                return;
            }

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _cmdService.ExecuteAsync(context, argPos, _services);
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // We have access to the information of the command executed,
            // the context of the command, and the result returned from the
            // execution in this event.

            // command is unspecified when there was a search failure (command not found)
            if (!command.IsSpecified)
            {
                if (!context.Message.Content.StartsWith(FergunConfig.GlobalPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"Unknown command: \"{context.Message.Content}\", sent by {context.User} in {(context.Channel is SocketDMChannel ? $"@{context.User.Username}#{context.User.Discriminator}" : $"{context.Guild.Name}/{context.Channel.Name}")}"));
                }
                return;
            }

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
                return;

            //await context.Channel.TriggerTypingAsync();
            double ignoreTime = 0.6;
            switch (result.Error)
            {
                //case CommandError.UnknownCommand:
                //    await SendCachedEmbedAsync(context.Message, string.Format(LocalizationService.Locate("CommandNotFound", context.Message), GetPrefix(context.Channel)));
                //    break;
                case CommandError.BadArgCount:
                    await SendEmbedAsync(context.Message, "\u26a0 " +
                        (command.Value.Module.Group == "aid"
                        ? string.Format(Localizer.Locate("BadArgumentCountAid", context.Channel), Localizer.GetPrefix(context.Channel))
                        : string.Format(Localizer.Locate("BadArgumentCount", context.Channel), Localizer.GetPrefix(context.Channel), command.Value.Name))
                        , _services);
                    break;

                case CommandError.ParseFailed:
                    await SendEmbedAsync(context.Message, "\u26a0 " +
                        (command.Value.Module.Group == "aid"
                        ? string.Format(Localizer.Locate("CommandParseFailedAid", context.Channel), Localizer.GetPrefix(context.Channel))
                        : string.Format(Localizer.Locate("CommandParseFailed", context.Channel), Localizer.GetPrefix(context.Channel), command.Value.Name))
                        , _services);
                    //await SendCachedEmbedAsync(context.Message, "\u26a0 " + string.Format(LocalizationService.Locate("CommandParseFailed", context.Channel), GetPrefix(context.Channel), command.Value.Name));
                    break;

                case CommandError.UnmetPrecondition when command.Value.Module.Name != "Dev":
                    if (context.Channel is SocketGuildChannel && context.Guild.Id == 264445053596991498 && result.ErrorReason == "This module is disabled in this guild.")
                    {
                        break;
                    }
                    // TODO: Cleanup
                    string errorReason = result.ErrorReason;
                    if (errorReason.StartsWith("RLMT", StringComparison.OrdinalIgnoreCase))
                    {
                        errorReason = errorReason.Substring(4);
                        ignoreTime = 4;
                    }
                    await SendEmbedAsync(context.Message, $"\u26a0 {Localizer.Locate(errorReason, context.Channel)}", _services);
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
                    reason = Localizer.Locate(reason, context.Channel);
                    await SendEmbedAsync(context.Message, $"\u26a0 {reason}", _services);
                    break;

                case CommandError.MultipleMatches:
                    await SendEmbedAsync(context.Message, $"\u26a0 {Localizer.Locate("MultipleMatches", context.Channel)}", _services);
                    break;

                case CommandError.Unsuccessful:
                    await SendEmbedAsync(context.Message, $"\u26a0 {result.ErrorReason}".Truncate(EmbedBuilder.MaxDescriptionLength), _services);
                    break;

                case CommandError.Exception when result is ExecuteResult execResult:
                    {
                        var exception = execResult.Exception;

                        var builder = new EmbedBuilder()
                            .WithTitle($"\u274c {Localizer.Locate("FailedExecution", context.Channel)} `{command.Value.Name}`")
                            .AddField(Localizer.Locate("ErrorType", context.Channel), $"```cs\n{exception.GetType().Name}```")
                            .AddField(Localizer.Locate("ErrorMessage", context.Channel), $"```cs\n{exception.Message}```")
                            .WithColor(FergunConfig.EmbedColor);

                        var owner = (await context.Client.GetApplicationInfoAsync()).Owner;

                        if (context.User.Id != owner.Id)
                        {
                            builder.WithFooter(Localizer.Locate("ErrorSentToOwner", context.Channel));
                        }

                        await SendEmbedAsync(context.Message, builder.Build(), _services);

                        // if the user that executed the command isn't the bot owner, send the full stack trace to the errors channel
                        if (context.User.Id != owner.Id)
                        {
                            var guild = await context.Client.GetGuildAsync(460627183501574144);
                            var channel = await guild.GetTextChannelAsync(696506593830895686);

                            string title = $"\u274c Failed to execute `{command.Value.Name}` in {(context.Channel is SocketDMChannel ? $"`{context.Channel.Name}`" : $"`{context.Guild.Name}`/`{context.Channel.Name}`")}";
                            var embed2 = new EmbedBuilder()
                                .WithTitle(title.Truncate(EmbedBuilder.MaxTitleLength))
                                .AddField(Localizer.Locate("ErrorType", context.Channel), $"```cs\n{exception.GetType().Name}```")
                                .AddField(Localizer.Locate("ErrorMessage", context.Channel), $"```cs\n{exception.ToString().Truncate(EmbedFieldBuilder.MaxFieldValueLength - 9)}```")
                                .AddField("Jump url", context.Message.GetJumpUrl())
                                .AddField("Command", context.Message.Content.Truncate(EmbedFieldBuilder.MaxFieldValueLength))
                                .WithColor(FergunConfig.EmbedColor);

                            await channel.SendMessageAsync(null, false, embed2.Build());
                        }
                    }
                    break;

                default:
                    break;
            }

            _ = IgnoreUserAsync(context.User.Id, TimeSpan.FromSeconds(ignoreTime));
            await _logService.LogAsync(new LogMessage(LogSeverity.Info, "Command", $"Failed to execute \"{command.Value.Name}\" for {context.User} in {(context.Channel is SocketDMChannel ? $"@{context.User.Username}#{context.User.Discriminator}" : $"{context.Guild.Name}/{context.Channel.Name}")}, with error type: {result.Error} and reason: {result.ErrorReason}"));
        }

        public static async Task IgnoreUserAsync(ulong id, TimeSpan time)
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

        private static Task<IUserMessage> SendEmbedAsync(IUserMessage userMessage, string embedText, IServiceProvider services, string text = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(FergunConfig.EmbedColor)
                .WithDescription(embedText)
                .Build();

            return SendEmbedAsync(userMessage, embed, services, text);
        }

        private static async Task<IUserMessage> SendEmbedAsync(IUserMessage userMessage, Embed embed, IServiceProvider services, string text = null)
        {
            IUserMessage response;
            bool found = services.GetRequiredService<CommandCacheService>().TryGetValue(userMessage.Id, out ulong messageId);

            if (found && (response = (IUserMessage)await userMessage.Channel.GetMessageAsync(messageId)) != null)
            {
                //if (response.Reactions.Count > 0)
                //{
                //    bool manageMessages = response.Author is IGuildUser guildUser && guildUser.GetPermissions((IGuildChannel)response.Channel).ManageMessages;

                //    // This can be slow hmm
                //    if (manageMessages)
                //        await response.RemoveAllReactionsAsync();
                //    else
                //        await response.RemoveReactionsAsync(response.Author, response.Reactions.Where(x => x.Value.IsMe).Select(x => x.Key).ToArray());
                //}
                await response.ModifyAsync(x =>
                {
                    x.Content = text;
                    x.Embed = embed;
                });
                response = (IUserMessage)await userMessage.Channel.GetMessageAsync(messageId);
            }
            else
            {
                response = await userMessage.Channel.SendMessageAsync(null, false, embed).ConfigureAwait(false);
                services.GetRequiredService<CommandCacheService>().Add(userMessage, response);
            }

            return response;
        }
    }
}