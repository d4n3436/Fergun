using Discord;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace Fergun;

internal class WolframAlphaPaginatorBuilder : BaseLazyPaginatorBuilder<WolframAlphaPaginator, WolframAlphaPaginatorBuilder>
{
    public new Func<int, int, IUserMessage?, IPageBuilder> PageFactory { get; set; } = null!;

    public WolframAlphaPaginatorBuilder WithPageFactory(Func<int, int, IUserMessage?, IPageBuilder> pageFactory)
    {
        PageFactory = pageFactory;
        return this;
    }

    public override WolframAlphaPaginator Build() => new(WithPageFactory(_ => null!));
}