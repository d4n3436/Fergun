using Discord;

namespace Fergun.Extensions;

public static class UserExtensions
{
    public static string Format(this IUser user) => user.DiscriminatorValue == 0 ? user.Username : $"{user.Username}#{user.Discriminator}";
}