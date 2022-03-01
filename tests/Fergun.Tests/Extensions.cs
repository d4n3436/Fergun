using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Fergun.Tests;

internal static class Extensions
{
    public static void SetPropertyValue<TSource, TProperty>(this TSource obj, Expression<Func<TSource, TProperty>> expression, TProperty newValue)
        => ((PropertyInfo)((MemberExpression)expression.Body).Member).SetValue(obj, newValue);
}