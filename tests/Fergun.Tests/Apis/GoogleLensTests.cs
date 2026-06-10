using System;
using System.Net.Http;
using System.Threading.Tasks;
using AutoBogus;
using Fergun.Apis.Google;
using Moq;
using Xunit;

namespace Fergun.Tests.Apis;

public class GoogleLensTests
{
    [Fact]
    public async Task Disposed_GoogleLens_Usage_Throws_ObjectDisposedException()
    {
        var googleLens = new GoogleLensClient();
        googleLens.Dispose();
        googleLens.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => googleLens.OcrAsync(AutoFaker.Generate<string>(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => googleLens.ReverseImageSearchAsync(AutoFaker.Generate<string>(), It.IsAny<string?>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GoogleLensException_Has_Expected_Values()
    {
        var innerException = new HttpRequestException();

        var exception1 = new GoogleLensException();
        var exception2 = new GoogleLensException("Custom message");
        var exception3 = new GoogleLensException("Custom message 2", innerException);

        Assert.Null(exception1.InnerException);

        Assert.Equal("Custom message", exception2.Message);
        Assert.Null(exception2.InnerException);

        Assert.Equal("Custom message 2", exception3.Message);
        Assert.Same(innerException, exception3.InnerException);
    }
}