using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Apis.Genius;
using Xunit;

namespace Fergun.Tests.Apis;

public class GeniusClientTests
{
    private readonly IGeniusClient _geniusClient = new GeniusClient(Utils.CreateMockedHttpClient());

    [Fact]
    public async Task GetSongAsync_Parses_Song_From_Response()
    {
        var client = new GeniusClient(Utils.CreateMockedHttpClient((HttpStatusCode.OK, GeniusTestData.SongResponse)));

        var song = await client.GetSongAsync(235729, TestContext.Current.CancellationToken);

        Assert.NotNull(song);
        Assert.Equal(235729, song.Id);
        Assert.Equal("Rap God", song.Title);
        Assert.Equal("Eminem", song.ArtistNames);
        Assert.False(song.IsInstrumental);
        Assert.Equal("6or1bKJiZ06IlK0vFvY75k", song.SpotifyTrackId);
        Assert.Equal("https://genius.com/artists/Eminem", song.PrimaryArtistUrl);
        Assert.NotNull(song.Lyrics);
        Assert.Contains("Look", song.Lyrics);
        Assert.Equal("Eminem - Rap God", song.ToString());
    }

    [Fact]
    public async Task GetSongAsync_Returns_Null_On_NotFound()
    {
        var client = new GeniusClient(Utils.CreateMockedHttpClient((HttpStatusCode.NotFound, string.Empty)));

        var song = await client.GetSongAsync(0, TestContext.Current.CancellationToken);

        Assert.Null(song);
    }

    [Fact]
    public async Task SearchSongsAsync_Parses_Hits_From_Response()
    {
        var client = new GeniusClient(Utils.CreateMockedHttpClient((HttpStatusCode.OK, GeniusTestData.SearchResponse)));

        var songs = await client.SearchSongsAsync("rap god", TestContext.Current.CancellationToken);

        Assert.NotEmpty(songs);
        Assert.Equal("Rap God", songs[0].Title);
        Assert.All(songs, s =>
        {
            Assert.NotEmpty(s.Title);
            Assert.NotEmpty(s.ArtistNames);
            // Search results never carry lyrics or a Spotify id.
            Assert.Null(s.Lyrics);
            Assert.Null(s.SpotifyTrackId);
        });
    }

    [Fact]
    public async Task Operations_Throw_OperationCanceledException_With_Canceled_Token()
    {
        using var cts = new CancellationTokenSource(0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _geniusClient.SearchSongsAsync("test", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => _geniusClient.GetSongAsync(0, cts.Token));
    }

    [Fact]
    public async Task Disposed_GeniusClient_Usage_Throws_ObjectDisposedException()
    {
        ((IDisposable)_geniusClient).Dispose();
        ((IDisposable)_geniusClient).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _geniusClient.SearchSongsAsync("test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _geniusClient.GetSongAsync(0, TestContext.Current.CancellationToken));
    }
}