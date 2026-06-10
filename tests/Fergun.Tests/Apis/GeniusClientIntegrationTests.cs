using System;
using System.Threading.Tasks;
using Fergun.Apis.Genius;
using JetBrains.Annotations;
using Xunit;

namespace Fergun.Tests.Apis;

[Trait("Category", "Integration")]
public class GeniusClientIntegrationTests
{
    private readonly IGeniusClient _geniusClient = new GeniusClient();

    [Theory]
    [InlineData("one")]
    [InlineData("music")]
    public async Task SearchSongsAsync_Returns_Valid_Songs(string query)
    {
        var results = await _geniusClient.SearchSongsAsync(query, TestContext.Current.CancellationToken);

        Assert.All(results, [AssertionMethod] (x) =>
        {
            Assert.NotEmpty(x.ArtistNames);
            Assert.True(x.Id > 0);
            Assert.NotEmpty(x.LyricsState);
            Assert.True(Uri.IsWellFormedUriString(x.SongArtImageUrl, UriKind.Absolute));
            Assert.NotEmpty(x.Title);
            Assert.True(Uri.IsWellFormedUriString(x.Url, UriKind.Absolute));
            Assert.True(Uri.IsWellFormedUriString(x.PrimaryArtistUrl, UriKind.Absolute));
            Assert.NotNull(x.ToString());

            // Null for SearchSongsAsync
            Assert.Null(x.SpotifyTrackId);
            Assert.Null(x.Lyrics);
        });
    }

    [Theory]
    [InlineData(235729, false, true, false)] // Eminem - Rap God
    [InlineData(2955220, false, false, false)] // Luis Fonsi (Ft. Daddy Yankee) - Despacito (dfp-unit, joined tags)
    [InlineData(8274090, false, false, true)] // The Killers - Amen (no lyrics, no spotify track id, unreleased)
    [InlineData(3925554, true, false, false)] // Alan Walker - Faded (Instrumental)
    public async Task GetSongAsync_Returns_Valid_Songs(int id, bool isInstrumental, bool hasSpotifyTrackId, bool isUnreleased)
    {
        var result = await _geniusClient.GetSongAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.ArtistNames);
        Assert.Equal(id, result.Id);
        Assert.Equal(isInstrumental, result.IsInstrumental);
        Assert.Equal(isUnreleased, result.LyricsState == "unreleased");
        Assert.True(Uri.IsWellFormedUriString(result.SongArtImageUrl, UriKind.Absolute));
        Assert.NotEmpty(result.Title);
        Assert.True(Uri.IsWellFormedUriString(result.Url, UriKind.Absolute));
        Assert.True(Uri.IsWellFormedUriString(result.PrimaryArtistUrl, UriKind.Absolute));
        Assert.NotNull(result.ToString());

        if (hasSpotifyTrackId)
        {
            Assert.Matches(@"[0-9A-Za-z]{22}", result.SpotifyTrackId);
        }

        if (!isInstrumental && !isUnreleased)
        {
            Assert.NotNull(result.Lyrics);
            Assert.NotEmpty(result.Lyrics);
        }
    }

    [Fact]
    public async Task GetSongAsync_With_Invalid_Id_Returns_Null_Song()
    {
        var result = await _geniusClient.GetSongAsync(0, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}