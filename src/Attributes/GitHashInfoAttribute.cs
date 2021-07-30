using System;

namespace Fergun.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class GitHashInfoAttribute : Attribute
    {
        public GitHashInfoAttribute(string gitHash)
        {
            GitHash = gitHash;
        }

        public string GitHash { get; }
    }
}