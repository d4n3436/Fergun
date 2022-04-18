using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using Discord;
using Fergun.Extensions;
using Moq;
using Xunit;

namespace Fergun.Tests.Extensions;

public class MessageExtensionsTests
{
    [Theory]
    [MemberData(nameof(GetRandomStrings))]
    public void IMessage_GetText_Should_Return_Text_From_Content(string content)
    {
        var messageMock = new Mock<IMessage>();
        messageMock.SetupGet(x => x.Content).Returns(content);
        messageMock.SetupGet(x => x.Embeds).Returns(Array.Empty<IEmbed>());

        string text = messageMock.Object.GetText();

        messageMock.VerifyGet(x => x.Content);
        Assert.Equal(content, text);
    }

    [Theory]
    [MemberData(nameof(GetContentsAndEmbeds))]
    public void IMessage_GetText_Should_Return_Text_From_Content_And_Embed(string content, Embed embed)
    {
        var messageMock = new Mock<IMessage>();
        messageMock.SetupGet(x => x.Content).Returns(content);
        messageMock.SetupGet(x => x.Embeds).Returns(new[] { embed });

        string[] parts = messageMock.Object.GetText().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        messageMock.VerifyGet(x => x.Content);
        messageMock.VerifyGet(x => x.Embeds);

        int index = 0;
        Assert.True(parts.Length >= 3);
        Assert.Equal(content, parts[index++]);
        if (embed.Author is not null)
        {
            Assert.Equal(embed.Author.Value.Name, parts[index++]);
        }
        Assert.Equal(embed.Title, parts[index++]);
        Assert.Equal(embed.Description, parts[index]);
        if (embed.Footer is not null)
        {
            Assert.Equal(embed.Footer.Value.Text, parts[^1]);
        }
    }

    private static IEnumerable<object[]> GetEmbeds()
    {
        return new Faker().MakeLazy(10, Utils.CreateFakeEmbedBuilder).Select(x => new object[] { x.Build() });
    }

    private static IEnumerable<object[]> GetContentsAndEmbeds()
    {
        return GetRandomStrings().Zip(GetEmbeds()).Select(x => new[] { x.First[0], x.Second[0] });
    }

    private static IEnumerable<object[]> GetRandomStrings()
    {
        var faker = new Faker();

        return faker.MakeLazy(10, () => faker.Random.String2(2))
            .Select(x => new object[] { x });
    }
}