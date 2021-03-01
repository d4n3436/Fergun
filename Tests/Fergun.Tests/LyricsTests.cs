using System.Threading.Tasks;
using Fergun.Utils;
using Xunit;

namespace Fergun.Tests
{
    public class LyricsTests
    {
        [Theory]
        [InlineData("https://genius.com/Luis-fonsi-despacito-lyrics", true)]
        [InlineData("https://genius.com/Eminem-rap-god-lyrics", true)]
        [InlineData("https://genius.com/Ed-sheeran-shape-of-you-lyrics", false)]
        [InlineData("https://genius.com/Queen-bohemian-rhapsody-lyrics", false)]
        public async Task LyricsAvailableTest(string url, bool keepHeaders)
        {
            // Act
            string lyrics = await CommandUtils.ParseGeniusLyricsAsync(url, keepHeaders);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(lyrics));
        }
    }
}