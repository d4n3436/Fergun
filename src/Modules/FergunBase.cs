using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Services;
using Fergun.Utils;

namespace Fergun.Modules
{
    /// <inheritdoc/>
    public abstract class FergunBase : FergunBase<ShardedCommandContext>
    {
    }

    /// <summary>
    /// The command module base that Fergun uses in its modules.
    /// </summary>
    public abstract class FergunBase<T> : CommandCacheModuleBase<T>
        where T : ShardedCommandContext
    {
        /// <summary>
        /// Gets or sets the interactive service.
        /// </summary>
        public InteractiveService Interactive { get; set; }

        /// <summary>
        /// Gets or sets the message cache service.
        /// </summary>
        public MessageCacheService MessageCache { get; set; }

        public bool DisplayRewriteWarning { get; } = Random.Shared.Next(100 + 1) <= GuildUtils.CachedRewriteWarnPercentage;

        /// <inheritdoc cref="InteractiveService.SendPaginatorAsync(Paginator, IMessageChannel, TimeSpan?, Action{IUserMessage}, bool, CancellationToken)"/>
        public async Task<InteractiveMessageResult> SendPaginatorAsync(Paginator paginator, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (Cache.TryGetValue(Context.Message.Id, out ulong messageId))
            {
                if (Interactive.TryRemoveCallback(messageId, out var callback))
                {
                    callback.Dispose();
                }

                var response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId).ConfigureAwait(false);

                return await Interactive.SendPaginatorAsync(paginator, response, timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return await Interactive.SendPaginatorAsync(paginator, Context.Channel, timeout, AddMessageToCache, cancellationToken: cancellationToken).ConfigureAwait(false);

            void AddMessageToCache(IUserMessage message)
            {
                if (!Cache.IsDisabled)
                {
                    Cache.Add(Context.Message, message);
                }
            }
        }

        /// <inheritdoc cref="InteractiveService.SendSelectionAsync{TOption}(BaseSelection{TOption}, IMessageChannel, TimeSpan?, Action{IUserMessage}, CancellationToken)"/>
        public async Task<InteractiveMessageResult<TOption>> SendSelectionAsync<TOption>(BaseSelection<TOption> selection,
            TimeSpan? timeout = null, IUserMessage message = null, CancellationToken cancellationToken = default)
        {
            ulong messageId = message?.Id ?? (Cache.TryGetValue(Context.Message.Id, out ulong temp) ? temp : 0);
            if (messageId != 0)
            {
                if (Interactive.TryRemoveCallback(messageId, out var callback))
                {
                    callback.Dispose();
                }

                var response = message ?? (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId).ConfigureAwait(false);

                return await Interactive.SendSelectionAsync(selection, response, timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return await Interactive.SendSelectionAsync(selection, Context.Channel, timeout, AddMessageToCache, cancellationToken).ConfigureAwait(false);

            void AddMessageToCache(IUserMessage msg)
            {
                if (!Cache.IsDisabled)
                {
                    Cache.Add(Context.Message, msg);
                }
            }
        }

        /// <inheritdoc/>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null,
            MessageReference messageReference = null, MessageComponent component = null, ISticker[] stickers = null, Embed[] embeds = null)
        {
            component ??= new ComponentBuilder().Build(); // remove message components if null

            if (Cache.IsDisabled)
            {
                return await base.ReplyAsync(message, isTTS, embed, options, allowedMentions, messageReference, component, stickers, embeds);
            }

            IUserMessage response;
            bool found = Cache.TryGetValue(Context.Message.Id, out ulong messageId);
            if (found && (response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId)) != null)
            {
                if (Interactive.TryRemoveCallback(messageId, out var callback))
                {
                    callback.Dispose();
                }

                await response.ModifyAsync(x =>
                {
                    x.Content = message;
                    x.Embed = embed;
                    x.Attachments = Array.Empty<FileAttachment>();
                    x.AllowedMentions = allowedMentions ?? Optional.Create<AllowedMentions>();
                    x.Components = component;
                }).ConfigureAwait(false);

                response = (IUserMessage)await Context.Channel.GetMessageAsync(MessageCache, messageId).ConfigureAwait(false);
            }
            else
            {
                if (DisplayRewriteWarning && GuildUtils.UserConfigCache.GetValueOrDefault(Context.User.Id, new UserConfig(Context.User.Id)).RewriteWarningExpirationTime < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    bool slashCommandEnabled = true;
                    bool slashCommandsScopeTested = Context.IsPrivate || GuildUtils.SlashCommandScopeCache.TryGetValue(Context.Guild.Id, out slashCommandEnabled);

                    if (!slashCommandsScopeTested)
                    {
                        try
                        {
                            await Context.Guild.GetApplicationCommandsAsync();
                            GuildUtils.SlashCommandScopeCache[Context.Guild.Id] = true;
                            slashCommandEnabled = true;
                        }
                        catch
                        {
                            // If it's not possible to get the guild slash commands, then slash commands are not enabled in that server
                            GuildUtils.SlashCommandScopeCache[Context.Guild.Id] = false;
                            slashCommandEnabled = false;
                        }
                    }

                    var warningEmbed = new EmbedBuilder()
                        .WithTitle(Locate("SwitchToSlashCommands"))
                        .WithDescription(slashCommandEnabled ? Locate("RewriteWarning") : Locate("RewriteWarningSlashCommandsNotEnabled"))
                        .WithColor(FergunClient.Config.EmbedColor)
                        .Build();

                    var componentBuilder = component.Components.Count == 0 ? new ComponentBuilder() : ComponentBuilder.FromComponents(component.Components);
                    int row = component.Components.Count == 0 ? 0 : 1;
                    if (!slashCommandEnabled)
                    {
                        componentBuilder.WithButton(Locate("EnableSlashCommands"), style: ButtonStyle.Link, url: FergunClient.AppCommandsAuthLink, row: row);
                    }

                    componentBuilder.WithButton(Locate("TempDisableWarning"), "disable_warning", ButtonStyle.Secondary, row: row);
                    if (component.Components.Count == 0)
                    {
                        componentBuilder.WithButton(Locate("SupportServer"), style: ButtonStyle.Link, url: FergunClient.Config.SupportServer, row: row);
                    }

                    component = componentBuilder.Build();

                    embeds = embed is null ? new[] { warningEmbed } : new[] { embed, warningEmbed };
                }
                else
                {
                    embeds = embed is null ? null : new[] { embed };
                }

                response = await Context.Channel.SendMessageAsync(message, isTTS, null, options, allowedMentions, messageReference, component, stickers, embeds).ConfigureAwait(false);
                Cache.Add(Context.Message, response);
            }
            return response;
        }

        /// <summary>
        /// Returns the prefix of the source channel.
        /// </summary>
        /// <returns>The prefix of the channel.</returns>
        public string GetPrefix() => GuildUtils.GetPrefix(Context.Channel);

        /// <summary>
        /// Returns the configuration of a guild using the source channel.
        /// </summary>
        /// <returns>The configuration of the guild, or <c>null</c> if the guild cannot be found in the database.</returns>
        public GuildConfig GetGuildConfig() => GuildUtils.GetGuildConfig(Context.Channel);

        /// <summary>
        /// Returns the language of the source channel.
        /// </summary>
        /// <returns>The language of the source channel.</returns>
        public string GetLanguage() => GuildUtils.GetLanguage(Context.Channel);

        /// <summary>
        /// Returns the localized value of a resource key.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public string Locate(string key) => GuildUtils.Locate(key, Context.Channel);

        /// <summary>
        /// Returns the localized value of a boolean.
        /// </summary>
        /// <param name="boolean">The boolean to localize.</param>
        /// <returns>The localized boolean.</returns>
        public string Locate(bool boolean) => GuildUtils.Locate(boolean ? "Yes" : "No", Context.Channel);

        /// <summary>
        /// Returns the localized value of a resource key in the specified language.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <param name="language">The language to localize the resource key.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public string Locate(string key, string language) => GuildUtils.Locate(key, language);

        /// <summary>
        /// Sends or edits an embed to the source channel, and adds the response to the cache if the message is new.
        /// </summary>
        /// <param name="text">The embed description.</param>
        /// <returns>A task that represents the send or edit operation. The task contains the sent or edited message.</returns>
        public async Task<IUserMessage> SendEmbedAsync(string text, bool displayRewriteWarning = false)
        {
            var builder = new EmbedBuilder()
                .WithDescription(text)
                .WithColor(FergunClient.Config.EmbedColor);

            if (displayRewriteWarning)
            {
                builder.AddField(Locate("CommandRemovalWarning"), Locate("MusicRemovalWarning"));
            }

            return await ReplyAsync(embed: builder.Build());
        }
    }
}