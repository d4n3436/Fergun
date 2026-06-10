using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Discord;
using Moq;
using Xunit;

namespace Fergun.Tests;

internal static class TestExtensions
{
    extension<T>(Mock<T> mock) where T : class, IDiscordInteraction
    {
        public void VerifyDeferAsync(bool ephemeral, Times times) => mock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), times);

        public void VerifyFollowupAsync(bool ephemeral, Times times) => mock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), times);

        public void VerifyFollowupWithFileAsync(string fileName, bool ephemeral, Times times) => mock.Verify(x => x.FollowupWithFileAsync(It.Is<FileAttachment>(f => f.FileName == fileName), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), times);

        public void VerifyFollowupWithFilesAsync(Times times) => mock.Verify(x => x.FollowupWithFilesAsync(It.IsAny<IEnumerable<FileAttachment>>(), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), times);

        public void VerifyRespondAsync(Expression<Func<Embed, bool>> embedMatch, Times times) => mock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.Is(embedMatch), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), times);
    }
    
    public static void VerifyDeferLoadingAsync<T>(this Mock<T> mock, bool ephemeral, Times times) where T : class, IComponentInteraction
        => mock.Verify(x => x.DeferLoadingAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), times);

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