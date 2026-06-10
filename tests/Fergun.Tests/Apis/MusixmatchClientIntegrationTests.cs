using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Musixmatch;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class MusixmatchClientIntegrationTests : IClassFixture<MusixmatchClientStateFixture>
{
    private readonly IMusixmatchClient _musixmatchClient;
    private readonly Mock<ILogger<MusixmatchClient>> _loggerMock = new();

    public MusixmatchClientIntegrationTests(MusixmatchClientStateFixture fixture) => _musixmatchClient = new MusixmatchClient(new HttpClient(), fixture.State, _loggerMock.Object);

    [Theory]
    [InlineData("thriller", true)]
    [InlineData("dance", false)]
    public async Task SearchSongsAsync_Returns_Valid_Songs(string query, bool onlyWithLyrics)
    {
        var results = await _musixmatchClient.SearchSongsAsync(query, onlyWithLyrics, CancellationToken.None);

        Assert.All(results, x =>
        {
            Assert.NotEmpty(x.ArtistName);
            Assert.True(x.Id >= 0);
            Assert.NotEmpty(x.SongArtImageUrl);
            Assert.True(Uri.IsWellFormedUriString(x.SongArtImageUrl, UriKind.Absolute));
            Assert.NotEmpty(x.Title);
            Assert.NotEmpty(x.Url);
            Assert.True(Uri.IsWellFormedUriString(x.Url, UriKind.Absolute));
            Assert.NotNull(x.ToString());

            // Null for SearchSongsAsync
            Assert.Null(x.ArtistUrl);
            Assert.Null(x.Lyrics);
            Assert.Null(x.SpotifyTrackId);

            if (onlyWithLyrics)
            {
                Assert.False(x.IsInstrumental);
                Assert.True(x.HasLyrics);
            }
        });
    }

    [Theory]
    [InlineData(208247352, false, false, false)] // Op - Under Attack (no lyrics, no cover image)
    [InlineData(84457474, true, true, true)] // Eminem - Without Me (restricted everywhere)
    public async Task GetSongAsync_Returns_Valid_Songs(int id, bool hasLyrics, bool hasSpotifyTrackId, bool restricted)
    {
        var result = await _musixmatchClient.GetSongAsync(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.ArtistName);
        Assert.True(result.Id >= 0);
        Assert.NotEmpty(result.SongArtImageUrl);
        Assert.True(Uri.IsWellFormedUriString(result.SongArtImageUrl, UriKind.Absolute));
        Assert.NotEmpty(result.Title);
        Assert.NotEmpty(result.Url);
        Assert.True(Uri.IsWellFormedUriString(result.Url, UriKind.Absolute));
        Assert.Equal(restricted, result.IsRestricted);
        Assert.NotNull(result.ToString());

        Assert.NotNull(result.ArtistUrl);
        Assert.True(Uri.IsWellFormedUriString(result.ArtistUrl, UriKind.Absolute));

        if (hasLyrics)
        {
            Assert.False(result.IsInstrumental);
            Assert.True(result.HasLyrics);
            Assert.NotNull(result.Lyrics);
        }

        if (hasSpotifyTrackId)
        {
            Assert.NotNull(result.SpotifyTrackId);
            Assert.Matches(@"[0-9A-Za-z]{22}", result.SpotifyTrackId);
        }
    }

    [Fact]
    public async Task GetSongAsync_With_Invalid_Id_Returns_Null_Song()
    {
        var result = await _musixmatchClient.GetSongAsync(0, CancellationToken.None);

        Assert.Null(result);
    }
}