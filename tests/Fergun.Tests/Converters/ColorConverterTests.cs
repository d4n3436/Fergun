using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Converters;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Color = System.Drawing.Color;

namespace Fergun.Tests.Converters;

public class ColorConverterTests
{
    [Fact]
    public void ColorConverter_GetDiscordType_Returns_String()
    {
        var converter = new ColorConverter();

        Assert.Equal(ApplicationCommandOptionType.String, converter.GetDiscordType());
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("abc", 0xabc)]
    [InlineData(" 2374692", 2374692)]
    [InlineData("#ff00ff", 0xff00ff)]
    [InlineData("0x136847", 0x136847)]
    [InlineData("0XFFFFFF", 0xFFFFFF)]
    [InlineData("&hb32821", 0xb32821)]
    [InlineData("&Hffff00", 0xffff00)]
    public async Task ColorConverter_ReadAsync_Returns_Successful_Result(string hexString, int expected)
    {
        var converter = new ColorConverter();
        var contextMock = new Mock<IInteractionContext>();
        var servicesMock = new Mock<IServiceProvider>();
        var optionMock = new Mock<IApplicationCommandInteractionDataOption>();
        optionMock.SetupGet(x => x.Value).Returns(() => hexString);

        var result = await converter.ReadAsync(contextMock.Object, optionMock.Object, servicesMock.Object);

        Assert.True(result.IsSuccess);
        var actual = Assert.IsType<Color>(result.Value);

        Assert.Equal(Color.FromArgb(expected), actual);

        servicesMock.Verify(x => x.GetService(It.IsAny<Type>()), Times.Never);
        optionMock.VerifyGet(x => x.Value, Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("test", "es")]
    [InlineData("one", "en")]
    [InlineData(".", "fr")]
    public async Task ColorConverter_ReadAsync_Returns_Unsuccessful_Result(string value, string locale)
    {
        var converter = new ColorConverter();
        var localizer = Utils.CreateMockedLocalizer<SharedResource>();

        var services = new ServiceCollection()
            .AddSingleton(localizer)
            .BuildServiceProvider();

        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns(() => locale);

        var contextMock = new Mock<IInteractionContext>();
        contextMock.SetupGet(x => x.Interaction).Returns(() => interactionMock.Object);

        var optionMock = new Mock<IApplicationCommandInteractionDataOption>();
        optionMock.SetupGet(x => x.Value).Returns(() => value);

        var result = await converter.ReadAsync(contextMock.Object, optionMock.Object, services);

        Assert.Equal(InteractionCommandError.ConvertFailed, result.Error);
        interactionMock.VerifyGet(x => x.UserLocale, Times.AtLeastOnce);
        contextMock.VerifyGet(x => x.Interaction, Times.AtLeastOnce);
        optionMock.VerifyGet(x => x.Value, Times.AtLeastOnce);
    }
}