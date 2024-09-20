using AutoBogus;
using Fergun.Apis.Genius;
using Moq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fergun.Tests.Apis;

public class GeniusClientTests
{
    private readonly IGeniusClient _geniusClient = new GeniusClient();

    [Theory]
    [InlineData("one")]
    [InlineData("music")]
    public async Task SearchSongsAsync_Returns_Valid_Songs(string query)
    {
        var results = await _geniusClient.SearchSongsAsync(query);

        Assert.All(results, x =>
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
    [InlineData(4661161, false, false, false)] // Juice WRLD - Unreleased Songs [Discography List] (header tags, no spotify track id, links)
    [InlineData(8274090, false, false, true)] // The Killers - Amen (no lyrics, no spotify track id, unreleased)
    [InlineData(3925554, true, false, false)] // Alan Walker - Faded (Instrumental)
    public async Task GetSongAsync_Returns_Valid_Songs(int id, bool isInstrumental, bool hasSpotifyTrackId, bool isUnreleased)
    {
        var result = await _geniusClient.GetSongAsync(id);

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
        var result = await _geniusClient.GetSongAsync(0);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchSongsAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        using var cts = new CancellationTokenSource(0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => _geniusClient.SearchSongsAsync(AutoFaker.Generate<string>(), cts.Token));
    }

    [Fact]
    public async Task GetSongAsync_Throws_OperationCanceledException_With_Canceled_CancellationToken()
    {
        using var cts = new CancellationTokenSource(0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => _geniusClient.GetSongAsync(It.IsAny<int>(), cts.Token));
    }

    [Fact]
    public async Task Disposed_GeniusClient_Usage_Throws_ObjectDisposedException()
    {
        (_geniusClient as IDisposable)?.Dispose();
        (_geniusClient as IDisposable)?.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _geniusClient.SearchSongsAsync(AutoFaker.Generate<string>(), It.IsAny<CancellationToken>()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _geniusClient.GetSongAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void GeniusException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new GeniusException();
        var exception2 = new GeniusException("Custom message");
        var exception3 = new GeniusException("Custom message 2", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);
    }
}