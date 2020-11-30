using System;

namespace Fergun.Attributes
{
    /// <summary>
    /// Attaches an example to your command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ExampleAttribute : Attribute
    {
        public string Example { get; }

        public ExampleAttribute(string example)
        {
            Example = example;
        }
    }
}