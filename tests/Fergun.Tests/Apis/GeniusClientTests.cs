using System;
using System.Threading;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.Genius;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class GeniusClientTests
{
    private readonly IGeniusClient _geniusClient = new GeniusClient(Utils.CreateMockedHttpClient());

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
}