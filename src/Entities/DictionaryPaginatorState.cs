using Fergun.Interactive;
using System.Collections.Generic;

namespace Fergun;

public class DictionaryPaginatorState
{
    public DictionaryPaginatorState(List<List<PageBuilder>> pages, IReadOnlyList<PageBuilder?> extraInformation)
    {
        Pages = pages;
        ExtraInformation = extraInformation;
    }

    public List<List<PageBuilder>> Pages { get; set; }

    public  IReadOnlyList<PageBuilder?> ExtraInformation { get; set; }

    public bool IsDisplayingExtraInfo { get; set; }

    public int CurrentCategoryIndex { get; set; }

    public int LastPageIndex { get; set; }
}