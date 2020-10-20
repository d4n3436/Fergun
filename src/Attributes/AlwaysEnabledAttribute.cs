using System;

namespace Fergun.Attributes
{
    /// <summary>
    /// Marks a command or module to be always enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    class AlwaysEnabledAttribute : Attribute
    {

    }
}