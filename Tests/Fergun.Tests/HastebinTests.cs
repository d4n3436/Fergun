using System.Threading.Tasks;
using Fergun.APIs;
using Xunit;

namespace Fergun.Tests
{
    public class HastebinTests
    {
        [Theory(Skip = "Hastebin is down")]
        [InlineData("hi")]
        [InlineData("The quick brown fox jumps over the lazy dog.")]
        [InlineData("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.")]
        public async Task HastebinAvailableTest(string content)
        {
            // Act
            var response = await Hastebin.UploadAsync(content);

            // Assert
            Assert.False(string.IsNullOrEmpty(response.Key));
        }
    }
}