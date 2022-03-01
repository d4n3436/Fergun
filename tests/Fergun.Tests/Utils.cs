using System;
using System.Globalization;
using System.Reflection;

namespace Fergun.Tests;

internal static class Utils
{
    public static T CreateInstance<T>(params object?[]? args) where T : class
        => (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null, args, CultureInfo.InvariantCulture)!;
}