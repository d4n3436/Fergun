using Fergun.Extensions;
using System;
using System.Linq;
using Xunit;

namespace Fergun.Tests.Extensions;

public class ListExtensionsTests
{
    [Fact]
    public void List_Shuffle_Returns_Expected_Order()
    {
        int[] arr = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        int[] shuffled = { 1, 5, 6, 9, 3, 2, 4, 7, 10, 8 };
        var rng = new Random(0);

        arr.Shuffle(rng);

        Assert.True(arr.SequenceEqual(shuffled));
    }
}