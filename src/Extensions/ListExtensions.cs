namespace Fergun.Extensions;

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list, Random? rng = null)
    {
        rng ??= Random.Shared;
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}