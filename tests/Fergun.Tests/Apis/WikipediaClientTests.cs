using System;
using System.Threading.Tasks;
using Fergun.Apis.Wikipedia;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class WikipediaClientTests
{
    [Fact]
    public async Task Disposed_WikipediaClient_Usage_Throws_ObjectDisposedException()
    {
        IWikipediaClient wikipediaClient = new WikipediaClient(Utils.CreateMockedHttpClient());
        ((IDisposable)wikipediaClient).Dispose();
        ((IDisposable)wikipediaClient).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => wikipediaClient.GetArticleAsync(It.IsAny<int>(), "en", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => wikipediaClient.SearchArticlesAsync("test", "en", TestContext.Current.CancellationToken));
    }
}