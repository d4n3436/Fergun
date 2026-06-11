using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Musixmatch;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests.Apis;

public class MusixmatchClientTests : IClassFixture<MusixmatchClientStateFixture>
{
    private readonly IMusixmatchClient _musixmatchClient;
    private readonly Mock<ILogger<MusixmatchClient>> _loggerMock = new();

    public MusixmatchClientTests(MusixmatchClientStateFixture fixture) => _musixmatchClient = new MusixmatchClient(new HttpClient(), fixture.State, _loggerMock.Object);

    [Fact]
    public async Task Operations_Throw_OperationCanceledException_With_Canceled_Token()
    {
        using var cts = new CancellationTokenSource(0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _musixmatchClient.SearchSongsAsync("test", false, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => _musixmatchClient.GetSongAsync(0, cts.Token));
    }

    [Theory]
    [MemberData(nameof(GetMockedMusixmatchClientSequences), DisableDiscoveryEnumeration = true)]
    public async Task GetSongAsync_Throws_MusixmatchException_When_Api_Calls_Are_Not_Successful(MusixmatchClient musixmatchClient)
    {
        await Assert.ThrowsAsync<MusixmatchException>(async () => await musixmatchClient.GetSongAsync(It.IsAny<int>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_SearchSongsAsync_Calls_Return_Valid_Results()
    {
        var successfulTokenResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    user_token = "token"
                }
            }
        };

        var successfulResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    track_list = new[]
                    {
                        new
                        {
                            track = new
                            {
                                artist_name = "Artist",
                                track_id = 1,
                                instrumental = false,
                                has_lyrics = true,
                                restricted = false,
                                album_coverart_500x500 = "https://example.com/image.png",
                                track_name = "Track",
                                track_share_url = "https://example.com/share"
                            }
                        }
                    }
                }
            }
        };

        var musixmatchMessageHandlerMock = new Mock<HttpMessageHandler>();

        musixmatchMessageHandlerMock
            .Protected()
            .As<HttpClient>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => GetResponseMessage(successfulResponse));

        var stateMessageHandlerMock = new Mock<HttpMessageHandler>();

        stateMessageHandlerMock
            .Protected()
            .As<HttpClient>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(2000);
                return GetResponseMessage(successfulTokenResponse);
            });

        var musixmatchHttpClient = new HttpClient(musixmatchMessageHandlerMock.Object);
        var stateHtpClient = new HttpClient(stateMessageHandlerMock.Object);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(stateHtpClient);

        var loggerMock = new Mock<ILogger<MusixmatchClient>>();

        var musixmatchClient = new MusixmatchClient(musixmatchHttpClient, new MusixmatchClientState(httpClientFactoryMock.Object), loggerMock.Object);

        var searchSongsTask1 = Task.Run(() => musixmatchClient.SearchSongsAsync("Test"));
        var searchSongsTask2 = Task.Run(() => musixmatchClient.SearchSongsAsync("Test 2"));

        await Task.WhenAll(searchSongsTask1, searchSongsTask2);

        var results1 = await searchSongsTask1;
        var results2 = await searchSongsTask2;

        Assert.NotEmpty(results1);
        Assert.NotEmpty(results2);
    }

    [Fact]
    public async Task Disposed_MusixmatchClient_Usage_Throws_ObjectDisposedException()
    {
        ((IDisposable)_musixmatchClient).Dispose();
        ((IDisposable)_musixmatchClient).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _musixmatchClient.SearchSongsAsync("test", false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _musixmatchClient.GetSongAsync(0, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("2", true)]
    public void BoolConverter_Returns_Expected_Results(string input, bool expectedResult)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BoolConverter());

        bool result = JsonSerializer.Deserialize<bool>(input, options);

        Assert.Equal(expectedResult, result);

        string serialized = JsonSerializer.Serialize(result, options);
        Assert.Equal(expectedResult ? "true" : "false", serialized);
    }

    [Theory]
    [InlineData("\"test\"")]
    [InlineData("null")]
    public void BoolConverter_Throws_InvalidOperationException(string input)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new BoolConverter());

        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Deserialize<bool>(input, options));
    }

    [Fact]
    public void MusixmatchException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new MusixmatchException();
        var exception2 = new MusixmatchException("Custom message 2");
        var exception3 = new MusixmatchException("Custom message 3", "captcha");
        var exception4 = new MusixmatchException("Custom message 4", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message 2", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 3", exception3.Message);
        Assert.Null(exception3.InnerException);
        Assert.Equal("captcha", exception3.Hint);

        Assert.Equal("Custom message 4", exception4.Message);
        Assert.Same(innerException, exception4.InnerException);

        var captchaException = Assert.Throws<MusixmatchException>(() => MusixmatchException.Throw(HttpStatusCode.Forbidden, "track.get", "captcha"));
        var renewException = Assert.Throws<MusixmatchException>(() => MusixmatchException.Throw(HttpStatusCode.Unauthorized, "lyrics.get", "renew"));
        var serverErrorException = Assert.Throws<MusixmatchException>(() => MusixmatchException.Throw(HttpStatusCode.InternalServerError, "token.get", null));

        Assert.Equal("captcha", captchaException.Hint);
        Assert.Equal("renew", renewException.Hint);
        Assert.Null(serverErrorException.Hint);
    }

    [Fact]
    public async Task SearchSongsAsync_Parses_Song_Fields_From_Response()
    {
        var response = new
        {
            message = new
            {
                header = new { status_code = 200 },
                body = new
                {
                    track_list = new[]
                    {
                        new
                        {
                            track = new
                            {
                                artist_name = "Eminem",
                                track_id = 84457474,
                                instrumental = false,
                                has_lyrics = true,
                                restricted = false,
                                album_coverart_500x500 = "https://example.com/cover.png",
                                track_name = "Without Me",
                                track_share_url = "https://example.com/share"
                            }
                        }
                    }
                }
            }
        };

        var client = CreateClientReturning(response);

        var results = await client.SearchSongsAsync("without me", false, TestContext.Current.CancellationToken);

        var song = Assert.Single(results);
        Assert.Equal("Eminem", song.ArtistName);
        Assert.Equal("Without Me", song.Title);
        Assert.Equal(84457474, song.Id);
        Assert.True(song.HasLyrics);
        Assert.False(song.IsInstrumental);
        Assert.True(Uri.IsWellFormedUriString(song.SongArtImageUrl, UriKind.Absolute));
        Assert.Equal("Eminem - Without Me", song.ToString());
    }

    // Builds a client whose Musixmatch endpoint returns the given response and whose state endpoint returns a valid token.
    private static MusixmatchClient CreateClientReturning(object musixmatchResponse)
    {
        var tokenResponse = new { message = new { header = new { status_code = 200 }, body = new { user_token = "token" } } };

        var musixmatchHandler = new Mock<HttpMessageHandler>();
        musixmatchHandler.Protected().As<HttpClient>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => GetResponseMessage(musixmatchResponse));

        var stateHandler = new Mock<HttpMessageHandler>();
        stateHandler.Protected().As<HttpClient>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => GetResponseMessage(tokenResponse));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(stateHandler.Object));

        return new MusixmatchClient(new HttpClient(musixmatchHandler.Object), new MusixmatchClientState(factory.Object), Mock.Of<ILogger<MusixmatchClient>>());
    }

    public static TheoryData<MusixmatchClient> GetMockedMusixmatchClientSequences()
    {
        var successfulTokenResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    user_token = "token"
                }
            }
        };

        var emptyTokenResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    user_token = ""
                }
            }
        };

        var invalidTokenResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    user_token = "UpgradeOnlyUpgradeOnlyUpgradeOnlyUpgradeOnly"
                }
            }
        };

        var renewTokenResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 200
                },
                body = new
                {
                    macro_calls = new[]
                    {
                        new
                        {
                            track_get = new
                            {
                                message = new
                                {
                                    header = new
                                    {
                                        status_code = 403,
                                        hint = "renew"
                                    },
                                    body = new { }
                                }
                            }
                        }
                    }
                }
            }
        };

        var serverErrorResponse = new
        {
            message = new
            {
                header = new
                {
                    status_code = 500,
                    hint = (string?)null
                },
                body = new { }
            }
        };

        var musixmatchMessageHandlerMock = new Mock<HttpMessageHandler>();

        musixmatchMessageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetResponseMessage(renewTokenResponse))
            .ReturnsAsync(GetResponseMessage(serverErrorResponse));

        var stateMessageHandlerMock = new Mock<HttpMessageHandler>();

        stateMessageHandlerMock
            .Protected()
            .As<HttpClient>()
            .SetupSequence(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetResponseMessage(emptyTokenResponse))
            .ReturnsAsync(GetResponseMessage(invalidTokenResponse))
            .ReturnsAsync(GetResponseMessage(successfulTokenResponse))
            .ReturnsAsync(GetResponseMessage(successfulTokenResponse));

        var musixmatchHttpClient = new HttpClient(musixmatchMessageHandlerMock.Object);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(stateMessageHandlerMock.Object));

        var loggerMock = new Mock<ILogger<MusixmatchClient>>();

        // the 1st and 2nd call return invalid tokens
        // the 3rd call returns valid token.get response and forbidden error in track.get,
        // triggering the retry policy and returning a new token.get response

        // We need clean MusixmatchClientStates (no rate-limits) for the tests
        return new TheoryData<MusixmatchClient>(
            GetNewClient(),
            GetNewClient(),
            GetNewClient());

        MusixmatchClient GetNewClient() => new(musixmatchHttpClient, new MusixmatchClientState(httpClientFactoryMock.Object), loggerMock.Object);
    }

    private static HttpResponseMessage GetResponseMessage<T>(T obj)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(obj)) };
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class MusixmatchClientStateFixture
{
    public MusixmatchClientStateFixture()
    {
        var services = new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider();

        State = new MusixmatchClientState(services.GetRequiredService<IHttpClientFactory>());
    }

    public MusixmatchClientState State { get; }
}