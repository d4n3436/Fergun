using System;
using System.Collections.Generic;
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
        public void QuestionsNotEmptyTest(uint amount, QuestionCategory category, QuestionDifficulty difficulty, QuestionType type, ResponseEncoding encoding)
        {
            // Act
            var response = TriviaApi.RequestQuestions(amount, category, difficulty, type, encoding);

            // Assert
            Assert.NotEmpty(response.Questions);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(300)]
        [InlineData(100)]
        public void QuestionsInvalidAmountTest(uint amount)
        {
            // Act and Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => TriviaApi.RequestQuestions(amount));
        }

        [Fact]
        public void CategoryListNotEmptyTest()
        {
            // Act
            var response = TriviaApi.RequestCategoryList();

            // Assert
            Assert.NotEmpty(response.CategoryList);
        }

        [Fact]
        public void GlobalQuestionCountNotEmptyTest()
        {
            // Act
            var response = TriviaApi.RequestGlobalQuestionCount();

            // Assert
            Assert.NotEmpty(response.CategoriesQuestionCount);
            Assert.NotNull(response.Overall);
        }

        [Theory]
        [MemberData(nameof(Categories))]
        public void NumberOfQuestionsInCategoryNotNullTest(QuestionCategory category)
        {
            // Arrange
            if (category == QuestionCategory.Any) return;

            // Act
            var response = TriviaApi.RequestNumberOfQuestionsInCategory(category);

            // Assert
            Assert.NotNull(response.CategoryQuestionCount);
        }

        [Fact]
        public void SessionTokenNotNullTest()
        {
            // Act
            var response = TriviaApi.SendSessionTokenCommand(TokenCommand.Request);

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