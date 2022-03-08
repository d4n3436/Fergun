using Fergun.Interactive.Pagination;

namespace Fergun.Interactive;

// builder for paginators with rewrite warning messages
public class WarningLazyPaginatorBuilder : BaseLazyPaginatorBuilder<WarningLazyPaginator, WarningLazyPaginatorBuilder>
{
    public override WarningLazyPaginator Build()
    {
        CacheLoadedPages = false;
        return new(this);
    }

    public WarningLazyPaginatorBuilder WithDisplayRewriteWarning(bool displayRewriteWarning)
    {
        DisplayRewriteWarning = displayRewriteWarning;
        return this;
    }

    public WarningLazyPaginatorBuilder WithLanguage(string language)
    {
        Language = language;
        return this;
    }

    public WarningLazyPaginatorBuilder WithSlashCommandsEnabled(bool slashCommandsEnabled)
    {
        SlashCommandsEnabled = slashCommandsEnabled;
        return this;
    }

    public bool DisplayRewriteWarning { get; set; }

    public string Language { get; set; }

    public bool SlashCommandsEnabled { get; set; }
}