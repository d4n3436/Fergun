using Fergun.Converters;
using System;
using Xunit;
using Discord;
using System.Linq;

namespace Fergun.Tests.Converters;

public class EmoteConverterTests
{
    private static readonly string[] _emojis = ["🙂", "ℹ️", "⚠️", "❌"];

    private static readonly string[] _emotes =
    [
        "<:run:1089707070009287310>",
        "<:LUL:961822449823572356>",
        "<a:loading:745805084655912213>",
        "<:thonk:893371354942652483>"
    ];

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int), false)]
    public void EmoteConverter_CanConvertFrom_Returns_Expected_Result(Type type, bool expected)
    {
        var converter = new EmoteConverter();

        bool result = converter.CanConvertFrom(null, type);

        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetConvertFromEmojiData))]
    public void EmoteConverter_ConvertFrom_Returns_Valid_Emojis(string str, Emoji expected)
    {
        var converter = new EmoteConverter();

        object converted = converter.ConvertFrom(null, null, str);

        var emoji = Assert.IsType<Emoji>(converted);

        Assert.Equal(expected.Name, emoji.Name);
    }

    [Theory]
    [MemberData(nameof(GetConvertFromEmoteData))]
    public void EmoteConverter_ConvertFrom_Returns_Valid_Emotes(string str, Emote expected)
    {
        var converter = new EmoteConverter();

        object converted = converter.ConvertFrom(null, null, str);

        var emote = Assert.IsType<Emote>(converted);

        Assert.Equal(expected.Name, emote.Name);
        Assert.Equal(expected.Id, emote.Id);
        Assert.Equal(expected.Animated, emote.Animated);
        Assert.Equal(expected.ToString(), emote.ToString());
    }

    [Theory]
    [InlineData((object?)null)]
    [InlineData("")]
    [InlineData(123)]
    [InlineData("<:fff:0158395")]
    public void EmoteConverter_ConvertFrom_Throws_Exception_On_Invalid_Emotes(object? obj)
    {
        var converter = new EmoteConverter();

        Assert.ThrowsAny<Exception>(() => converter.ConvertFrom(null, null, obj!));
    }

    public static TheoryData<string, Emoji> GetConvertFromEmojiData() => _emojis.Select(x => (x, new Emoji(x))).ToTheoryData();

    public static TheoryData<string, Emote> GetConvertFromEmoteData() => _emotes.Select(x => (x, Emote.Parse(x))).ToTheoryData();
}