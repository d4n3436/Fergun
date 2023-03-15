using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Extensions;
using Fergun.Interactive.Pagination;
using Fergun.Modules;

namespace Fergun;

/// <summary>
/// Represents a custom paginator for <see cref="UtilityModule.DefineAsync(string)"/>.
/// </summary>
public class DictionaryPaginator : BaseLazyPaginator
{
    private readonly IReadOnlyList<int> _maxCategoryIndexes;
    private readonly IReadOnlyList<IPage?> _extraInformation;
    private Dictionary<PaginatorAction, string?>? _actions;
    private bool _isDisplayingExtraInfo;

    /// <inheritdoc />
    public DictionaryPaginator(DictionaryPaginatorBuilder builder, IReadOnlyList<IPage?> extraInformation,
        IReadOnlyList<int> maxCategoryIndexes, int customMaxPageIndex) : base(builder)
    {
        _extraInformation = extraInformation;
        _maxCategoryIndexes = maxCategoryIndexes;
        MaxPageIndex = customMaxPageIndex;
        MaxCategoryIndex = _maxCategoryIndexes[0];
    }

    /// <summary>
    /// Gets the current part of speech index within an entry.
    /// </summary>
    public int CurrentCategoryIndex { get; set; }

    /// <summary>
    /// Gets the maximun part of speech index within an entry.
    /// </summary>
    public int MaxCategoryIndex { get; set; }

    /// <inheritdoc cref="Paginator.MaxPageIndex"/>
    public new int MaxPageIndex { get; }

    /// <inheritdoc/>
    public override ComponentBuilder GetOrAddComponents(bool disableAll, ComponentBuilder? builder = null)
    {
        _actions ??= new Dictionary<PaginatorAction, string?>
        {
            { PaginatorAction.SkipToStart, "Previous definition" },
            { PaginatorAction.Backward, "Previous category"},
            { PaginatorAction.Forward, "Next category "},
            { PaginatorAction.SkipToEnd, "Next definition" }, // words can have different IPA pronuctiation like bass
            { PaginatorAction.Jump, "More information" },
            { PaginatorAction.Exit, null }
        };

        builder ??= new ComponentBuilder();

        int row = 0;
        foreach (var pair in Emotes)
        {
            if (pair.Value == PaginatorAction.Jump && _extraInformation[CurrentPageIndex] is null)
            {
                continue;
            }

            /*
            if (pair.Value is PaginatorAction.Backward or PaginatorAction.Forward && MaxCategoryIndex == 0)
            {
                continue;
            }

            if (pair.Value is PaginatorAction.SkipToStart or PaginatorAction.SkipToEnd && MaxPageIndex == 0)
            {
                continue;
            }
            */
            bool isDisabled = disableAll || pair.Value switch
            {
                PaginatorAction.SkipToStart => _isDisplayingExtraInfo || CurrentPageIndex == 0,
                PaginatorAction.Backward => _isDisplayingExtraInfo || CurrentCategoryIndex == 0,
                PaginatorAction.Forward => _isDisplayingExtraInfo || CurrentCategoryIndex == MaxCategoryIndex,
                PaginatorAction.SkipToEnd => _isDisplayingExtraInfo || CurrentPageIndex == MaxPageIndex,
                _ => false
            };

            var style = pair.Value switch
            {
                PaginatorAction.Exit => ButtonStyle.Danger,
                PaginatorAction.Jump => ButtonStyle.Secondary,
                _ => ButtonStyle.Primary
            };

            string? label = pair.Value == PaginatorAction.Jump && _isDisplayingExtraInfo ? "Go back" : _actions[pair.Value];

            var button = new ButtonBuilder()
                .WithCustomId(pair.Key.ToString())
                .WithStyle(style)
                .WithEmote(pair.Key)
                .WithLabel(label)
                .WithDisabled(isDisabled);

            builder.WithButton(button, row / 2);
            row++;
        }

        return builder;
    }

    /// <inheritdoc/>
    public override ValueTask<bool> ApplyActionAsync(PaginatorAction action) =>
        action switch
        {
            PaginatorAction.Backward => SetCategoryIndex(CurrentCategoryIndex - 1),
            PaginatorAction.Forward => SetCategoryIndex(CurrentCategoryIndex + 1),
            PaginatorAction.SkipToStart => SetPageAsync(CurrentPageIndex - 1),
            PaginatorAction.SkipToEnd => SetPageAsync(CurrentPageIndex + 1),
            PaginatorAction.Jump => new ValueTask<bool>(true),
            _ => new ValueTask<bool>(false)
        };

    public override ValueTask<bool> SetPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || CurrentPageIndex == pageIndex || pageIndex > MaxPageIndex)
        {
            return ValueTask.FromResult(false);
        }

        CurrentPageIndex = pageIndex;
        CurrentCategoryIndex = 0;
        MaxCategoryIndex = _maxCategoryIndexes[CurrentPageIndex];

        return ValueTask.FromResult(true);
    }

    private ValueTask<bool> SetCategoryIndex(int index)
    {
        CurrentCategoryIndex = index;
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public override async Task<InteractiveInputResult> HandleInteractionAsync(SocketMessageComponent input, IUserMessage message)
    {
        if (!InputType.HasFlag(InputType.Buttons))
        {
            return InteractiveInputStatus.Ignored;
        }

        if (input.Message.Id != message.Id || !this.CanInteract(input.User))
        {
            return InteractiveInputStatus.Ignored;
        }

        var emote = (input
                .Message
                .Components
                .SelectMany(x => x.Components)
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

        var extraInfo = _extraInformation[CurrentPageIndex];
        if (action == PaginatorAction.Jump && extraInfo is not null)
        {
            if (_isDisplayingExtraInfo)
            {
                _isDisplayingExtraInfo = false;
            }
            else
            {
                _isDisplayingExtraInfo = true;

                var buttons = GetOrAddComponents(false).Build();

                try
                {
                    await input.UpdateAsync(x =>
                    {
                        x.Embeds = extraInfo.GetEmbedArray();
                        x.Components = buttons;
                    });
                }
                catch
                {
                    _isDisplayingExtraInfo = false;
                }

                return InteractiveInputStatus.Success;
            }
        }

        bool refreshPage = await ApplyActionAsync(action).ConfigureAwait(false);
        if (refreshPage)
        {
            var currentPage = await GetOrLoadCurrentPageAsync().ConfigureAwait(false);
            var attachments = currentPage.AttachmentsFactory is null ? null : await currentPage.AttachmentsFactory().ConfigureAwait(false);
            var buttons = GetOrAddComponents(false).Build();

            await input.UpdateAsync(x =>
            {
                x.Content = currentPage.Text;
                x.Embeds = currentPage.GetEmbedArray();
                x.Components = buttons;
                x.AllowedMentions = currentPage.AllowedMentions;
                x.Attachments = attachments is null ? new Optional<IEnumerable<FileAttachment>>() : new Optional<IEnumerable<FileAttachment>>(attachments);
            }).ConfigureAwait(false);
        }

        return InteractiveInputStatus.Success;
    }
}