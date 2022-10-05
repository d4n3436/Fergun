using Discord;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace Fergun;

internal class WolframAlphaPaginator : BaseLazyPaginator
{
    public WolframAlphaPaginator(WolframAlphaPaginatorBuilder builder)
        : base(builder)
    {
        PageFactory = builder.PageFactory;
    }

    public IUserMessage CurrentMessage { get; private set; } = null!;

    public int LastIndex { get; private set; } = -1;

    public new Func<int, int, IUserMessage?, IPageBuilder> PageFactory { get; }

    public override Task<IPage> GetOrLoadPageAsync(int pageIndex) => Task.FromResult(PageFactory(pageIndex, LastIndex, CurrentMessage).Build());

    public override Task<InteractiveInputResult> HandleInteractionAsync(SocketMessageComponent input, IUserMessage message)
    {
        CurrentMessage = input.Message;
        LastIndex = CurrentPageIndex;

        return base.HandleInteractionAsync(input, message);
    }
}