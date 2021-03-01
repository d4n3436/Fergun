using System.Net;
using Fergun.Responses;
using Newtonsoft.Json;
using Xunit;

namespace Fergun.Tests
{
    public class XkcdTests
    {
        [Fact]
        public void XkcdAvailableTest()
        {
            // Arrange
            using var wc = new WebClient();

            // Act
            string response = wc.DownloadString("https://xkcd.com/info.0.json"); // Last comic
            var comic = JsonConvert.DeserializeObject<XkcdComic>(response);

            // Assert
            Assert.NotNull(comic);

            // Act
            response = wc.DownloadString($"https://xkcd.com/{comic.Num - 1}/info.0.json"); // Previous comic
            var prevComic = JsonConvert.DeserializeObject<XkcdComic>(response);

            // Assert
            Assert.NotNull(prevComic);
        }
    }
}