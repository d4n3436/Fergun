using System.Threading.Tasks;
using Fergun.APIs.DuckDuckGo;
using Xunit;

namespace Fergun.Tests
{
    public class DuckDuckGoTests
    {
        [Theory]
        [InlineData("Hello world", SafeSearch.Off)]
        [InlineData("Discord", SafeSearch.Moderate)]
        [InlineData("Dogs", SafeSearch.Strict)]
        [InlineData("Cats", SafeSearch.Strict)]
        public async Task SearchNotEmptyTest(string keywords, SafeSearch filter)
        {
            // Act
            var results = await DdgApi.SearchImagesAsync(keywords, filter);

            // Assert
            Assert.NotEmpty(results.Results);
        }

        // Triggers !bangs
        [Theory]
        [InlineData("Hello w!orld")]
        [InlineData("!b")]
        [InlineData("!wikipedia")]
        [InlineData("!q")]
        [InlineData("d!rive")]
        public async Task SearchInvalidTest(string keywords)
        {
            // Act and Assert
            await Assert.ThrowsAsync<TokenNotFoundException>(async () => await DdgApi.SearchImagesAsync(keywords));
        }
    }
}