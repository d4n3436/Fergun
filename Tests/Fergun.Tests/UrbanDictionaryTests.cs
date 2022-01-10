using System.Threading.Tasks;
using Fergun.APIs.UrbanDictionary;
using Xunit;

namespace Fergun.Tests
{
    public class UrbanDictionaryTests
    {
        [Theory]
        [InlineData("lol")]
        [InlineData("afaik")]
        [InlineData("cringe")]
        [InlineData("pog")]
        [InlineData("gg")]
        public async Task SearchNotEmptyTest(string word)
        {
            // Act
            var response = await UrbanApi.SearchWordAsync(word);

            // Assert
            Assert.NotEmpty(response.Definitions);
        }

        [Fact]
        public async Task RandomSearchNotEmptyTest()
        {
            // Act
            var response = await UrbanApi.GetRandomWordsAsync();

            // Assert
            Assert.NotEmpty(response.Definitions);
        }
    }
}