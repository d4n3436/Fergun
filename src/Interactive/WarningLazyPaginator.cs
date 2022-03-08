using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Fergun.Interactive.Extensions;
using Fergun.Interactive.Pagination;
using Fergun.Utils;

namespace Fergun.Interactive;

public class WarningLazyPaginator : BaseLazyPaginator
{
    public WarningLazyPaginator(WarningLazyPaginatorBuilder properties) : base(properties)
    {
        DisplayRewriteWarning = properties.DisplayRewriteWarning;
        Language = properties.Language;
        SlashCommandsEnabled = properties.SlashCommandsEnabled;
    }

    public bool DisplayRewriteWarning { get; private set; }

    public string Language { get; }

    public bool SlashCommandsEnabled { get; }

    public override ComponentBuilder GetOrAddComponents(bool disableAll, ComponentBuilder builder = null)
    {
        builder = base.GetOrAddComponents(disableAll, builder);

        if (DisplayRewriteWarning)
        {
            builder.WithButton(GuildUtils.Locate("TempDisableWarning", Language), "disable_warning_int", ButtonStyle.Secondary, disabled: disableAll || !DisplayRewriteWarning, row: 1);

            if (!SlashCommandsEnabled)
            {
                builder.WithButton(GuildUtils.Locate("EnableSlashCommands", Language), style: ButtonStyle.Link, url: FergunClient.AppCommandsAuthLink, row: 1);
            }

            builder.WithButton(GuildUtils.Locate("SupportServer", Language), style: ButtonStyle.Link, url: FergunClient.Config.SupportServer, row: 1);
        }

        return builder;
    }

    public override async Task<IPage> GetOrLoadCurrentPageAsync()
    {
        var page = await base.GetOrLoadCurrentPageAsync();

        return new WarningPage(page, DisplayRewriteWarning, SlashCommandsEnabled, Language, MaxPageIndex == 0);
    }

    public class WarningPage : IPage
    {
        private readonly IPage _page;
        private readonly bool _showWarning;
        private readonly bool _slashCommandsEnabled;
        private readonly string _language;
        private readonly bool _onePage;

        public WarningPage(IPage page, bool showWarning, bool slashCommandsEnabled, string language, bool onePage)
        {
            _page = page;
            _showWarning = showWarning;
            _slashCommandsEnabled = slashCommandsEnabled;
            _language = language;
            _onePage = onePage;
        }

        public string Text => _page.Text;

        public IReadOnlyCollection<Embed> Embeds => _page.Embeds;

        public Embed[] GetEmbedArray()
        {
            if (!_showWarning || _onePage)
                return Embeds.ToArray();

            var warningEmbed = new EmbedBuilder()
                .WithTitle(GuildUtils.Locate("SwitchToSlashCommands", _language))
                .WithDescription(_slashCommandsEnabled ? GuildUtils.Locate("RewriteWarning", _language) : GuildUtils.Locate("RewriteWarningSlashCommandsNotEnabled", _language))
                .WithColor(FergunClient.Config.EmbedColor)
                .Build();

            return Embeds.Append(warningEmbed).ToArray();
        }
    }

    public override async Task<InteractiveInputResult> HandleInteractionAsync(SocketMessageComponent input, IUserMessage message)
    {
        if (!InputType.HasFlag(InputType.Buttons))
        {
            return new(InteractiveInputStatus.Ignored);
        }

        if (input.Message.Id != message.Id || !this.CanInteract(input.User))
        {
            return new(InteractiveInputStatus.Ignored);
        }

        if (input.Data.CustomId == "disable_warning_int")
        {
            ulong userId = input.User.Id;
            var userConfig = GuildUtils.UserConfigCache.GetValueOrDefault(userId, new UserConfig(userId));
            var expirationDate = DateTimeOffset.FromUnixTimeSeconds(userConfig.RewriteWarningExpirationTime);

            if (expirationDate < DateTimeOffset.UtcNow)
            {
                userConfig.RewriteWarningExpirationTime = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
                FergunClient.Database.InsertOrUpdateDocument(Constants.UserConfigCollection, userConfig);
                GuildUtils.UserConfigCache[userId] = userConfig;

                Console.WriteLine($"Disabled rewrite warning temporarily for user {input.User} ({userId}).");
                await input.RespondAsync(GuildUtils.Locate("RewriteWarningDisabled", input.Channel), ephemeral: true);
                DisplayRewriteWarning = false;

                var currentPage = await GetOrLoadCurrentPageAsync().ConfigureAwait(false);

                await input.Message.ModifyAsync(x =>
                {
                    x.Content = currentPage.Text;
                    x.Embeds = currentPage.GetEmbedArray();
                    x.Components = GetOrAddComponents(false).Build();
                });
            }
            else
            {
                // Got expiration date in the future, this means the user pressed the warning button and the warning is already disabled
                await input.DeferAsync();
            }

            return new(InteractiveInputStatus.Ignored);
        }

        var emote = (input
                .Message
                .Components
                .FirstOrDefault()?
                .Components?
                .FirstOrDefault(x => x is ButtonComponent button && button.CustomId == input.Data.CustomId) as ButtonComponent)?
            .Emote;

        if (emote is null || !Emotes.TryGetValue(emote, out var action))
        {
            return InteractiveInputStatus.Ignored;
        }

        if (action == PaginatorAction.Exit)
        {
            return InteractiveInputStatus.Canceled;
        }

        bool refreshPage = await ApplyActionAsync(action).ConfigureAwait(false);
        if (refreshPage)
        {
            var currentPage = await GetOrLoadCurrentPageAsync().ConfigureAwait(false);
            var buttons = GetOrAddComponents(false).Build();

            await input.UpdateAsync(x =>
            {
                x.Content = currentPage.Text;
                x.Embeds = currentPage.GetEmbedArray();
                x.Components = buttons;
            }).ConfigureAwait(false);
        }

        return InteractiveInputStatus.Success;
    }
}