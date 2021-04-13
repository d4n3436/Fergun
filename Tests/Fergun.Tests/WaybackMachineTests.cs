using System;
using System.Threading.Tasks;
using Fergun.APIs.WaybackMachine;
using Xunit;

namespace Fergun.Tests
{
    public class WaybackMachineTests
    {
        [Theory]
        [InlineData("google.com", 2000)]
        [InlineData("youtube.com", 2009)]
        [InlineData("facebook.com", 2015)]
        [InlineData("twitter.com", 2020)]
        public async Task SnapshotNotNullTest(string url, ulong timestamp)
        {
            // Act
            var result = await WaybackApi.GetSnapshotAsync(url, timestamp);

            // Assert
            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("", 2010)]
        [InlineData(null, 2020)]
        public async Task SnapshotInvalidUrlTest(string url, ulong timestamp)
        {
            // Act and Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await WaybackApi.GetSnapshotAsync(url, timestamp));
        }

        [Theory]
        [InlineData("google.com", 1)]
        [InlineData("youtube.com", 100000000000000)]
        public async Task SnapshotInvalidTimestampTest(string url, ulong timestamp)
        {
            // Act and Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await WaybackApi.GetSnapshotAsync(url, timestamp));
        }
    }
}