using System;

namespace Fergun.Attributes
{
    /// <summary>
    /// Attribute for example commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    class ExampleAttribute : Attribute
    {
        public string Example
        {
            get;
        }

        public ExampleAttribute(string example)
        {
            Example = example;
        }
    }
}