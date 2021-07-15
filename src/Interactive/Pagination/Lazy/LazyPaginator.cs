using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace Fergun.Interactive.Pagination
{
    /// <summary>
    /// Represents a lazy paginator.
    /// </summary>
    public sealed class LazyPaginator : Paginator
    {
        private readonly Dictionary<int, Page> _cachedPages;

        /// <summary>
        /// Gets the function used to load the pages of this paginator lazily.
        /// </summary>
        public Func<int, Task<Page>> PageFactory { get; }

        /// <inheritdoc/>
        public override int MaxPageIndex { get; }

        /// <summary>
        /// Gets whether to cache loaded pages.
        /// </summary>
        public bool CacheLoadedPages { get; }

        internal LazyPaginator(IReadOnlyCollection<IUser> users, IReadOnlyDictionary<IEmote, PaginatorAction> emotes,
            Page canceledPage, Page timedOutPage, DeletionOptions deletion, InputType inputType,
            ActionOnStop actionOnCancellation, ActionOnStop actionOnTimeout, Func<int, Task<Page>> pageFactory,
            int startPage, int maxPageIndex, bool cacheLoadedPages)
            : base(users, emotes, canceledPage, timedOutPage, deletion, inputType, actionOnCancellation, actionOnTimeout, startPage)
        {
            PageFactory = pageFactory;
            MaxPageIndex = maxPageIndex;
            CacheLoadedPages = cacheLoadedPages;

            if (CacheLoadedPages)
            {
                _cachedPages = new Dictionary<int, Page>();
            }
        }

        /// <inheritdoc/>
        public override async Task<Page> GetOrLoadPageAsync(int pageIndex)
        {
            if (CacheLoadedPages && _cachedPages != null && _cachedPages.TryGetValue(pageIndex, out var page))
            {
                return page;
            }

            page = await PageFactory(pageIndex).ConfigureAwait(false);
            _cachedPages?.TryAdd(pageIndex, page);

            return page;
        }
    }
}