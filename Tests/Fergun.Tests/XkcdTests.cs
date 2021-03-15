using System.Net.Http;
using System.Threading.Tasks;
using Fergun.Responses;
using Newtonsoft.Json;
using Xunit;

namespace Fergun.Tests
{
    public class XkcdTests
    {
        [Fact]
        public async Task XkcdAvailableTest()
        {
            // Arrange
            using var httpClient = new HttpClient();

            // Act
            string response = await httpClient.GetStringAsync("https://xkcd.com/info.0.json"); // Last comic
            var comic = JsonConvert.DeserializeObject<XkcdComic>(response);

            // Assert
            Assert.NotNull(comic);

            // Act
            response = await httpClient.GetStringAsync($"https://xkcd.com/{comic.Num - 1}/info.0.json"); // Previous comic
            var prevComic = JsonConvert.DeserializeObject<XkcdComic>(response);

            // Assert
            Assert.NotNull(prevComic);
        }
    }
}