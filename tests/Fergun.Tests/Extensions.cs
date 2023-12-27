using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace Fergun.Tests;

internal static class TestExtensions
{
    public static void SetPropertyValue<TSource, TProperty>(this TSource obj, Expression<Func<TSource, TProperty>> expression, TProperty newValue)
        => ((PropertyInfo)((MemberExpression)expression.Body).Member).SetValue(obj, newValue);

    public static TheoryData<T> ToTheoryData<T>(this IEnumerable<T> objects)
    {
        var data = new TheoryData<T>();
        foreach (var obj in objects)
        {
            data.Add(obj!);
        }

        return data;
    }

    public static TheoryData<T1, T2> ToTheoryData<T1, T2>(this IEnumerable<(T1, T2)> objects)
    {
        var data = new TheoryData<T1, T2>();
        foreach (var (p1, p2) in objects)
        {
            data.Add(p1, p2);
        }

        return data;
    }

    public static TheoryData<T1, T2, T3> ToTheoryData<T1, T2, T3>(this IEnumerable<(T1, T2, T3)> objects)
    {
        var data = new TheoryData<T1, T2, T3>();
        foreach (var (p1, p2, p3) in objects)
        {
            data.Add(p1, p2, p3);
        }

        return data;
    }
}