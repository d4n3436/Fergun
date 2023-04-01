using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;

namespace Fergun.Preconditions;

/// <summary>
/// Requires the user to not be ratelimited on a module or command.
/// </summary>
public class RatelimitAttribute : PreconditionAttribute
{
    private readonly int _times;
    private readonly TimeSpan _period;
    private readonly ConcurrentDictionary<ulong, RatelimitInfo> _ratelimits = new();
    private static ulong _ownerId;

    /// <summary>
    /// Initializes a new insance of the <see cref="RatelimitAttribute"/> class.
    /// </summary>
    /// <param name="times">The number of times the command can be executed per <paramref name="period"/>.</param>
    /// <param name="period">How often the executions are permitted.</param>
    /// <param name="measure">The measure of time <paramref name="period"/> is interpreted to be.</param>
    public RatelimitAttribute(int times, double period, TimeMeasure measure = TimeMeasure.Seconds)
    {
        _times = times;
        _period = measure switch
        {
            TimeMeasure.Seconds => TimeSpan.FromSeconds(period),
            TimeMeasure.Minutes => TimeSpan.FromMinutes(period),
            TimeMeasure.Hours => TimeSpan.FromHours(period),
            _ => throw new ArgumentException("Invalid time measure value.", nameof(period))
        };
    }

    /// <inheritdoc/>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        if (_ownerId == 0)
            _ownerId = (await context.Client.GetApplicationInfoAsync()).Owner.Id;

        ulong userId = context.User.Id;

        if (userId == _ownerId)
            return PreconditionResult.FromSuccess();

        var ratelimit = _ratelimits.GetOrAdd(userId, static _ => new RatelimitInfo());

        var now = DateTimeOffset.UtcNow;
        var timePassed = now - ratelimit.FirstUsage;

        if (ratelimit.UsageCount >= _times && timePassed <= _period)
        {
            var timeLeft = _period - timePassed;
            var localizer = services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());

            return PreconditionResult.FromError(localizer["Ratelimited", timeLeft.Humanize(culture: localizer.CurrentCulture)]);
        }

        if (timePassed > _period)
        {
            ratelimit.Reset();
        }

        ratelimit.UsageCount++;

        return PreconditionResult.FromSuccess();
    }

    private sealed class RatelimitInfo
    {
        public int UsageCount { get; set; }

        public DateTimeOffset FirstUsage { get; private set; } = DateTimeOffset.UtcNow;

        public void Reset()
        {
            UsageCount = 0;
            FirstUsage = DateTimeOffset.UtcNow;
        }
    }
}