using Discord;
using Fergun.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Fergun.Tests.Extensions;

public class ExtensionsTests
{
    [Theory]
    [InlineData(LogSeverity.Critical, LogLevel.Critical)]
    [InlineData(LogSeverity.Error, LogLevel.Error)]
    [InlineData(LogSeverity.Warning, LogLevel.Warning)]
    [InlineData(LogSeverity.Info, LogLevel.Information)]
    [InlineData(LogSeverity.Verbose, LogLevel.Debug)]
    [InlineData(LogSeverity.Debug, LogLevel.Trace)]
    public void LogSeverity_ToLogLevel_Returns_Expected_Result(LogSeverity input, LogLevel output)
    {
        var result = input.ToLogLevel();

        Assert.Equal(output, result);
    }

    [Fact]
    public void LogSeverity_ToLogLevel_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("logSeverity", () => ((LogSeverity)(-1)).ToLogLevel());
    }

    [Theory]
    [MemberData(nameof(GetIInteractionContextDisplayTestData))]
    public void IInteractionContext_Display_Contains_Required_Info(IInteractionContext context)
    {
        string result = context.Display();

        if (context.Channel is IGuildChannel guildChannel)
        {
            Assert.Contains(guildChannel.Guild.Name, result);
        }
        else if (context.Channel is not null)
        {
            Assert.Contains(context.Channel.Name, result);
        }
        else
        {
            Assert.Contains(context.Interaction.ChannelId.ToString()!, result);
        }
    }

    [Fact]
    public void Object_Dump_Returns_Expected_Result()
    {
        const string serialized = "{\r\n  \"one\": {\r\n    \"two\": {}\r\n  }\r\n}";

        var depth2Obj = new
        {
            one = new
            {
                two = new
                {
                }
            }
        };

        var depth3Obj = new
        {
            one = new
            {
                two = new
                {
                    test = "removed"
                }
            }
        };

        string depth2Result = depth2Obj.Dump();
        string depth3Result = depth3Obj.Dump();

        Assert.Equal(serialized, depth2Result);
        Assert.Equal(serialized, depth3Result);
    }

    public static IEnumerable<object[]> GetIInteractionContextDisplayTestData()
    {
        var guildMock = new Mock<IGuild>();
        guildMock.SetupGet(x => x.Name)
            .Returns("Test Guild");

        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.ChannelId)
            .Returns(123);

        var textChannelMock = new Mock<ITextChannel>();
        textChannelMock.SetupGet(x => x.Guild)
            .Returns(guildMock.Object);

        textChannelMock.SetupGet(x => x.Name)
            .Returns("Test message channel");

        var messageChannelMock = new Mock<IMessageChannel>();
        messageChannelMock.SetupGet(x => x.Name)
            .Returns("Test message channel");

        var contextMock1 = new Mock<IInteractionContext>();
        contextMock1.SetupGet(x => x.Channel)
            .Returns((IMessageChannel)null!);

        contextMock1.SetupGet(x => x.Interaction)
            .Returns(interactionMock.Object);

        var contextMock2 = new Mock<IInteractionContext>();
        contextMock2.SetupGet(x => x.Channel)
            .Returns(textChannelMock.Object);

        contextMock2.SetupGet(x => x.Interaction)
            .Returns(interactionMock.Object);

        var contextMock3 = new Mock<IInteractionContext>();
        contextMock3.SetupGet(x => x.Channel)
            .Returns(messageChannelMock.Object);

        contextMock3.SetupGet(x => x.Interaction)
            .Returns(interactionMock.Object);

        yield return new object[] { contextMock1.Object };
        yield return new object[] { contextMock2.Object };
        yield return new object[] { contextMock3.Object };
    }
}