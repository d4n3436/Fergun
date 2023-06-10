using Discord;
using Fergun.Extensions;
using System.Collections.Generic;
using Xunit;

namespace Fergun.Tests.Extensions;

public class UserExtensionsTests
{
    [Theory]
    [MemberData(nameof(GetUserTestData))]
    public void User_Format_Returns_Expected_Result(IUser user, bool isPomelo)
    {
        string formatted = user.Format();

        Assert.Equal(!isPomelo, formatted.Contains('#'));
    }

    public static IEnumerable<object[]> GetUserTestData()
    {
        return new[]
        {
            new object[] { Utils.CreateMockedUser(), false },
            new object[] { Utils.CreateMockedUser(null, true), true },
        };
    }
}