using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fergun.APIs.OpenTriviaDB;
using Xunit;

namespace Fergun.Tests
{
    public class OpenTriviaDbTests
    {
        [Theory]
        [InlineData(50, QuestionCategory.Any, QuestionDifficulty.Any, QuestionType.Any, ResponseEncoding.Default)]
        [InlineData(10, QuestionCategory.Computers, QuestionDifficulty.Easy, QuestionType.Multiple, ResponseEncoding.url3986)]
        [InlineData(30, QuestionCategory.VideoGames, QuestionDifficulty.Hard, QuestionType.Multiple, ResponseEncoding.base64)]
        [InlineData(1, QuestionCategory.Animals, QuestionDifficulty.Medium, QuestionType.Boolean, ResponseEncoding.urlLegacy)]
        public async Task QuestionsNotEmptyTest(uint amount, QuestionCategory category, QuestionDifficulty difficulty, QuestionType type, ResponseEncoding encoding)
        {
            // Act
            var response = await TriviaApi.RequestQuestionsAsync(amount, category, difficulty, type, encoding);

            // Assert
            Assert.NotEmpty(response.Questions);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(300)]
        [InlineData(100)]
        public async Task QuestionsInvalidAmountTest(uint amount)
        {
            // Act and Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await TriviaApi.RequestQuestionsAsync(amount));
        }

        [Fact]
        public async Task CategoryListNotEmptyTest()
        {
            // Act
            var response = await TriviaApi.RequestCategoryListAsync();

            // Assert
            Assert.NotEmpty(response.CategoryList);
        }

        [Fact]
        public async Task GlobalQuestionCountNotEmptyTest()
        {
            // Act
            var response = await TriviaApi.RequestGlobalQuestionCountAsync();

            // Assert
            Assert.NotEmpty(response.CategoriesQuestionCount);
            Assert.NotNull(response.Overall);
        }

        [Theory]
        [MemberData(nameof(Categories))]
        public async Task NumberOfQuestionsInCategoryNotNullTest(QuestionCategory category)
        {
            // Arrange
            if (category == QuestionCategory.Any) return;

            // Act
            var response = await TriviaApi.RequestNumberOfQuestionsInCategoryAsync(category);

            // Assert
            Assert.NotNull(response.CategoryQuestionCount);
        }

        [Fact]
        public async Task SessionTokenNotNullTest()
        {
            // Act
            var response = await TriviaApi.SendSessionTokenCommandAsync(TokenCommand.Request);

            // Assert
            Assert.NotNull(response.Token);
        }

        public static IEnumerable<object[]> Categories()
        {
            foreach (var value in Enum.GetValues(typeof(QuestionCategory)))
            {
                yield return new[] { value };
            }
        }
    }
}