using System;
using System.Collections.Generic;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace Fergun;

/// <summary>
/// Represents the builder of <see cref="DictionaryPaginator"/>.
/// </summary>
public class DictionaryPaginatorBuilder : BaseLazyPaginatorBuilder<DictionaryPaginator, DictionaryPaginatorBuilder>
{
    private IReadOnlyList<IPage?>? _extraInformation;
    private IReadOnlyList<int>? _maxCategoryIndexes;

    /// <summary>
    /// Sets the extra information.
    /// </summary>
    /// <param name="extraInformation">The extra information.</param>
    /// <returns>This builder.</returns>
    public DictionaryPaginatorBuilder WithExtraInformation(IReadOnlyList<IPage?> extraInformation)
    {
        _extraInformation = extraInformation;
        return this;
    }

    /// <summary>
    /// Sets the initial maximum category index.
    /// </summary>
    /// <param name="maxCategoryIndexes">The initial maximum category index.</param>
    /// <returns>This builder.</returns>
    public DictionaryPaginatorBuilder WithMaxCategoryIndexes(IReadOnlyList<int> maxCategoryIndexes)
    {
        _maxCategoryIndexes = maxCategoryIndexes;
        return this;
    }

    /// <inheritdoc />
    public override DictionaryPaginator Build()
    {
        ArgumentNullException.ThrowIfNull(_extraInformation);
        ArgumentNullException.ThrowIfNull(_maxCategoryIndexes);

        int customMaxPageIndex = MaxPageIndex;
        if (MaxPageIndex == 0)
        {
            MaxPageIndex++;
        }

        return new DictionaryPaginator(this, _extraInformation, _maxCategoryIndexes, customMaxPageIndex);
    }
}