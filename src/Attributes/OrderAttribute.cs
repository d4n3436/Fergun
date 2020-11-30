using System;

namespace Fergun.Attributes
{
    /// <summary>
    /// Marks the order of a module, a lower value equals higher order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OrderAttribute : Attribute
    {
        public int Order { get; }

        public OrderAttribute(int order)
        {
            Order = order;
        }
    }
}