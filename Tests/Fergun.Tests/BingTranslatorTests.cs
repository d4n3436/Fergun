using System;
using System.Threading.Tasks;
using Fergun.APIs.BingTranslator;
using Xunit;

namespace Fergun.Tests
{
    public class BingTranslatorTests
    {
        [Theory]
        [InlineData("Hello World", "es")]
        [InlineData("Hola Mundo", "en")]
        [InlineData("The quick brown fox jumps over the lazy dog.", "it")]
        [InlineData("El zorro marrón rápido salta sobre el perro perezoso.", "pt")]
        [InlineData("Discord is an American VoIP, instant messaging and digital distribution platform designed for creating communities.", "de", "en")]
        public async Task TranslationNotEmptyTest(string text, string toLanguage, string fromLanguage = "auto-detect")
        {
            // Act
            var results = await BingTranslatorApi.TranslateAsync(text, toLanguage, fromLanguage);

            // Assert
            Assert.NotEmpty(results);
            Assert.NotEmpty(results[0].Translations);
        }

        [Theory]
        [InlineData("", "en")]
        [InlineData("Hello world", "eng")]
        [InlineData("object", "it", "po")]
        public async Task TranslationInvalidParamsTest(string text, string toLanguage, string fromLanguage = "auto-detect")
        {
            // Act and Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await BingTranslatorApi.TranslateAsync(text, toLanguage, fromLanguage));
        }
    }
}