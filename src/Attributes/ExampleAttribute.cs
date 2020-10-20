using System;

namespace Fergun.Attributes
{
    /// <summary>
    /// Attaches an example to your command.
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