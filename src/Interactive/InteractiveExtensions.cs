using Discord;

namespace Fergun.Interactive
{
    internal static class InteractiveExtensions
    {
        public static bool CanInteract<TOption>(this IInteractiveElement<TOption> element, IUser user)
            => element != null && user != null && CanInteract(element, user.Id);

        public static bool CanInteract<TOption>(this IInteractiveElement<TOption> element, ulong userId)
        {
            if (element.Users == null || element.Users.Count == 0)
            {
                return true;
            }

            foreach (var user in element.Users)
            {
                if (user.Id == userId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}