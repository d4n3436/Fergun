using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Services;

namespace Fergun.Attributes.Preconditions
{
    /// <summary>
    ///     Sets how often a user is allowed to use this command
    ///     or any command in this module.
    /// </summary>
    /// <remarks>
    ///     <note type="warning">
    ///         This is backed by an in-memory collection
    ///         and will not persist with restarts.
    ///     </note>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class RatelimitAttribute : PreconditionAttribute
    {
        /// <inheritdoc />
        public override string ErrorMessage { get; set; }
        public uint InvokeLimit { get; }
        public TimeSpan InvokeLimitPeriod { get; }

        private readonly bool _noLimitInDMs;
        private readonly bool _noLimitForAdmins;
        private readonly bool _applyPerGuild;
        private readonly Dictionary<(ulong, ulong?), CommandTimeout> _invokeTracker = new Dictionary<(ulong, ulong?), CommandTimeout>();

        /// <summary>
        ///     Sets how often a user is allowed to use this command. </summary>
        /// <param name="times">
        ///     The number of times a user may use the command within a certain period.
        /// </param>
        /// <param name="period">
        ///     The amount of time since first invoke a user has until the limit is lifted.
        /// </param>
        /// <param name="measure">
        ///     The scale in which the <paramref name="period"/> parameter should be measured.
        /// </param>
        /// <param name="flags">
        ///     Flags to set behavior of the ratelimit.
        /// </param>
        public RatelimitAttribute(
            uint times, double period, Measure measure,
            RatelimitFlags flags = RatelimitFlags.None)
        {
            InvokeLimit = times;
            _noLimitInDMs = (flags & RatelimitFlags.NoLimitInDMs) == RatelimitFlags.NoLimitInDMs;
            _noLimitForAdmins = (flags & RatelimitFlags.NoLimitForAdmins) == RatelimitFlags.NoLimitForAdmins;
            _applyPerGuild = (flags & RatelimitFlags.ApplyPerGuild) == RatelimitFlags.ApplyPerGuild;

            InvokeLimitPeriod = measure switch
            {
                Measure.Days => TimeSpan.FromDays(period),
                Measure.Hours => TimeSpan.FromHours(period),
                Measure.Minutes => TimeSpan.FromMinutes(period),
                _ => throw new ArgumentOutOfRangeException(paramName: nameof(period),
                    message: "Argument was not within the valid range.")
            };
        }

        /// <summary>
        ///     Sets how often a user is allowed to use this command.
        /// </summary>
        /// <param name="times">
        ///     The number of times a user may use the command within a certain period.
        /// </param>
        /// <param name="period">
        ///     The amount of time since first invoke a user has until the limit is lifted.
        /// </param>
        /// <param name="flags">
        ///     Flags to set bahavior of the ratelimit.
        /// </param>
        /// <remarks>
        ///     <note type="warning">
        ///         This is a convinience constructor overload for use with the dynamic
        ///         command builders, but not with the Class &amp; Method-style commands.
        ///     </note>
        /// </remarks>
        public RatelimitAttribute(
            uint times, TimeSpan period,
            RatelimitFlags flags = RatelimitFlags.None)
        {
            InvokeLimit = times;
            _noLimitInDMs = (flags & RatelimitFlags.NoLimitInDMs) == RatelimitFlags.NoLimitInDMs;
            _noLimitForAdmins = (flags & RatelimitFlags.NoLimitForAdmins) == RatelimitFlags.NoLimitForAdmins;
            _applyPerGuild = (flags & RatelimitFlags.ApplyPerGuild) == RatelimitFlags.ApplyPerGuild;

            InvokeLimitPeriod = period;
        }

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo _, IServiceProvider __)
        {
            if ((await context.Client.GetApplicationInfoAsync()).Owner.Id == context.User.Id)
                return PreconditionResult.FromSuccess();

            if (_noLimitInDMs && context.Channel is IPrivateChannel)
                return PreconditionResult.FromSuccess();

            if (_noLimitForAdmins && context.User is IGuildUser gu && gu.GuildPermissions.Administrator)
                return PreconditionResult.FromSuccess();

            var now = DateTime.UtcNow;
            var key = _applyPerGuild ? (context.User.Id, context.Guild?.Id) : (context.User.Id, null);

            var timeout = (_invokeTracker.TryGetValue(key, out var t)
                && ((now - t.FirstInvoke) < InvokeLimitPeriod))
                    ? t : new CommandTimeout(now);

            timeout.TimesInvoked++;

            if (timeout.TimesInvoked <= InvokeLimit)
            {
                _invokeTracker[key] = timeout;
                return PreconditionResult.FromSuccess();
            }
            else
            {
                // i think this is the way..?
                var result = (InvokeLimitPeriod - (now - t.FirstInvoke)).TotalSeconds;
                //Console.WriteLine($"_invokeLimitPeriod.Seconds: {_invokeLimitPeriod.TotalSeconds}\n" +
                //    //$"_invokeLimit: {_invokeLimit}\n" +
                //    $"now - t.FirstInvoke: {(now - t.FirstInvoke).TotalSeconds}\n" + 
                //    $"result: {result}\n\nrounded: {Math.Round(result, 2)}");

                return PreconditionResult.FromError(
                    ErrorMessage ?? "RLMT" + string.Format(Localizer.Locate("Ratelimited", context.Channel), Math.Round(result, 2).ToString()));
            }
        }

        private sealed class CommandTimeout
        {
            public uint TimesInvoked { get; set; }
            public DateTime FirstInvoke { get; }

            public CommandTimeout(DateTime timeStarted)
            {
                FirstInvoke = timeStarted;
            }
        }
    }

    /// <summary>
    ///     Determines the scale of the period parameter.
    /// </summary>
    public enum Measure
    {
        /// <summary>
        ///     Period is measured in days.
        /// </summary>
        Days,

        /// <summary>
        ///     Period is measured in hours.
        /// </summary>
        Hours,

        /// <summary>
        ///     Period is measured in minutes.
        /// </summary>
        Minutes
    }

    /// <summary>
    ///     Determines the behavior of the <see cref="RatelimitAttribute"/>.
    /// </summary>
    [Flags]
    public enum RatelimitFlags
    {
        /// <summary>
        ///     Set none of the flags.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Set whether or not there is no limit to the command in DMs.
        /// </summary>
        NoLimitInDMs = 1 << 0,

        /// <summary>
        ///     Set whether or not there is no limit to the command for guild admins.
        /// </summary>
        NoLimitForAdmins = 1 << 1,

        /// <summary>
        ///     Set whether or not to apply a limit per guild.
        /// </summary>
        ApplyPerGuild = 1 << 2
    }
}
