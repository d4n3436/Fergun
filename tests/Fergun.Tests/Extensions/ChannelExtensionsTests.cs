using Discord;
using Fergun.Extensions;
using Moq;
using Xunit;

namespace Fergun.Tests.Extensions;

public class ChannelExtensionsTests
{
    [Fact]
    public void IMessageChannel_IsNsfw_Should_Return_True_When_Channel_Is_NSFW()
    {
        var channelMock1 = new Mock<ITextChannel>();
        channelMock1.SetupGet(x => x.IsNsfw).Returns(true);

        var channelMock2 = new Mock<ITextChannel>();
        channelMock2.SetupGet(x => x.IsNsfw).Returns(false);

        var channelMock3 = new Mock<IDMChannel>();

        Assert.True(channelMock1.Object.IsNsfw());
        Assert.False(channelMock2.Object.IsNsfw());
        Assert.False(channelMock3.Object.IsNsfw());
    }

    [Fact]
    public void IChannel_IsPrivate_Should_Return_True_When_Channel_Is_IPrivateChannel()
    {
        var channelMock1 = new Mock<IChannel>();
        var channelMock2 = new Mock<ITextChannel>();
        var channelMock3 = new Mock<IDMChannel>();

        Assert.False(channelMock1.Object.IsPrivate());
        Assert.False(channelMock2.Object.IsPrivate());
        Assert.True(channelMock3.Object.IsPrivate());
    }
}