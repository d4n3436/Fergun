using System;

namespace Fergun.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class TestPriorityAttribute : Attribute
    {
        public TestPriorityAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; }
    }
}