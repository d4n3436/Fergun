﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using Bogus.DataSets;
using Discord;
using Discord.Interactions;
using Fergun.Converters;
using GTranslate;
using GTranslate.Translators;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests.Converters;

public class MicrosoftVoiceConverterTests
{
    private static readonly byte[] _microsoftTokenResponse = """{"r":"westus","t":"dGVzdA==.eyJleHAiOjIxNDc0ODM2NDd9.dGVzdA=="}"""u8.ToArray();

    [Fact]
    public void MicrosoftVoiceConverter_GetDiscordType_Returns_String()
    {
        var converter = new MicrosoftVoiceConverter();

        Assert.Equal(ApplicationCommandOptionType.String, converter.GetDiscordType());
    }

    [Theory]
    [MemberData(nameof(GetMicrosoftVoiceTestData))]
    public async Task MicrosoftVoiceConverter_ReadAsync_Returns_Successful_Result(MicrosoftVoice voice, bool isDefault)
    {
        var microsoftTranslator = CreateMockedMicrosoftTranslator(() =>
        {
            if (isDefault)
            {
                var cts = new CancellationTokenSource(TimeSpan.Zero);
                return Task.FromCanceled<HttpResponseMessage>(cts.Token);
            }

            byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(new[] { voice });
            return Task.FromResult(GetResponseMessage(serialized));
        });

        var services = new ServiceCollection()
            .AddSingleton(microsoftTranslator)
            .BuildServiceProvider();

        if (!isDefault)
        {
            await microsoftTranslator.GetTTSVoicesAsync();
        }

        var converter = new MicrosoftVoiceConverter();
        var contextMock = new Mock<IInteractionContext>();
        var optionMock = new Mock<IApplicationCommandInteractionDataOption>();
        optionMock.SetupGet(x => x.Value).Returns(() => voice.ShortName);

        var result = await converter.ReadAsync(contextMock.Object, optionMock.Object, services);

        Assert.True(result.IsSuccess);
        var actual = Assert.IsType<MicrosoftVoice>(result.Value);
        Assert.Equal(voice.ShortName, actual.ShortName);

        Assert.Equal(isDefault, MicrosoftTranslator.DefaultVoices.Values.Select(x => x.ShortName).Contains(voice.ShortName));
        optionMock.VerifyGet(x => x.Value, Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "es")]
    [InlineData("\u200b", "it")]
    public async Task MicrosoftVoiceConverter_ReadAsync_Returns_Unsuccessful_Result(string? voice, string locale)
    {
        var microsoftTranslator = CreateMockedMicrosoftTranslator(() => Task.FromCanceled<HttpResponseMessage>(CancellationToken.None));
        var localizer = Utils.CreateMockedLocalizer<SharedResource>();

        var services = new ServiceCollection()
            .AddSingleton(microsoftTranslator)
            .AddSingleton(localizer)
            .BuildServiceProvider();

        var converter = new MicrosoftVoiceConverter();

        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns(() => locale);

        var contextMock = new Mock<IInteractionContext>();
        contextMock.SetupGet(x => x.Interaction).Returns(() => interactionMock.Object);

        var optionMock = new Mock<IApplicationCommandInteractionDataOption>();
        optionMock.SetupGet(x => x.Value).Returns(() => voice!);

        var result = await converter.ReadAsync(contextMock.Object, optionMock.Object, services);

        Assert.Equal(InteractionCommandError.ConvertFailed, result.Error);
        interactionMock.VerifyGet(x => x.UserLocale, Times.AtLeastOnce);
        contextMock.VerifyGet(x => x.Interaction, Times.AtLeastOnce);
        optionMock.VerifyGet(x => x.Value, Times.AtLeastOnce);
    }

    private static MicrosoftTranslator CreateMockedMicrosoftTranslator(Func<Task<HttpResponseMessage>> getVoicesFunc)
    {
        var messageHandlerMock = new Mock<HttpMessageHandler>();

        messageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => GetResponseMessage(_microsoftTokenResponse))
            .Returns(getVoicesFunc);

        return new MicrosoftTranslator(new HttpClient(messageHandlerMock.Object));
    }

    private static HttpResponseMessage GetResponseMessage(byte[] data) => new(HttpStatusCode.OK) { Content = new ReadOnlyMemoryContent(data) };

    public static TheoryData<MicrosoftVoice, bool> GetMicrosoftVoiceTestData()
    {
        var faker = new Faker();
        var fakeVoices = faker.MakeLazy(10, () =>
        {
            var gender = faker.PickRandom<Name.Gender>();
            string locale = faker.Random.RandomLocale().Replace('_', '-');
            string displayName = faker.Name.FirstName(gender);
            string genderStr = gender.ToString();

            return new MicrosoftVoice(displayName, $"{locale}-{displayName}Neural", genderStr, locale);
        }).Select(x => (x, false));

        return MicrosoftTranslator.DefaultVoices.Values
            .Select(x => (x, true))
            .Concat(fakeVoices)
            .ToTheoryData();
    }
}