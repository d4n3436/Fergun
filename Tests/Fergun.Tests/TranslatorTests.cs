using System;
using System.Threading.Tasks;
using GTranslate.Translators;
using Xunit;

namespace Fergun.Tests
{
    public class TranslatorTests
    {
        [Theory]
        [InlineData("Hello World", "es")]
        [InlineData("Hola Mundo", "en")]
        [InlineData("The quick brown fox jumps over the lazy dog.", "it")]
        [InlineData("El zorro marrón rápido salta sobre el perro perezoso.", "pt")]
        [InlineData("Discord is an American VoIP, instant messaging and digital distribution platform designed for creating communities.", "de", "en")]
        public async Task TranslationNotEmptyTest(string text, string toLanguage, string fromLanguage = null)
        {
            // Arrange
            using var translator = new Translator();

            // Act
            var translation = await translator.TranslateAsync(text, toLanguage, fromLanguage);

            // Assert
            Assert.NotEmpty(translation.Result);
        }

        [Theory]
        [InlineData("", "en")]
        [InlineData("Hello world", null)]
        [InlineData("object", "", "po")]
        public async Task TranslationInvalidParamsTest(string text, string toLanguage, string fromLanguage = null)
        {
            // Arrange
            using var translator = new Translator();

            // Act and Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await translator.TranslateAsync(text, toLanguage, fromLanguage));
        }
    }
}