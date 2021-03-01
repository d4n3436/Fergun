using System.Threading.Tasks;
using GScraper;
using Xunit;

namespace Fergun.Tests
{
    public class GScraperTests
    {
        [Theory]
        [InlineData("Hello world", 100, true)]
        [InlineData("Discord", 50, false)]
        [InlineData("Dogs", 200, true)]
        [InlineData("Cats", 1, true)]
        public async Task GScraperAvailableTest(string query, int limit, bool safeSearch)
        {
            // Arrange
            var scraper = new GoogleScraper();

            // Act
            var results = await scraper.GetImagesAsync(query, limit, safeSearch);

            // Assert
            Assert.NotEmpty(results);
        }
    }
}