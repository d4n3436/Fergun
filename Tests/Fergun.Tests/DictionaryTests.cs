using System.Threading.Tasks;
using Fergun.APIs.Dictionary;
using Xunit;

namespace Fergun.Tests
{
    public class DictionaryTests
    {
        [Theory]
        [InlineData("make", "en", false)]
        [InlineData("day", "en", false)]
        [InlineData("tabla", "es", false)]
        [InlineData("jeux", "fr", false)]
        [InlineData("mixture", "ru", true)]
        [InlineData("result", "de", true)]
        public async Task ResultNotEmptyTest(string word, string language, bool fallback)
        {
            // Act
            var results = await DictionaryApi.GetDefinitionsAsync(word, language, fallback);

            // Assert
            Assert.NotEmpty(results);
        }
    }
}