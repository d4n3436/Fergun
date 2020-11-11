using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    public class ReactionCallbackData
    {
        public ReactionCallbackData(string text, Embed embed = null, bool expiresAfterUse = true, bool singleUsePerUser = true, TimeSpan? timeout = null, Func<SocketCommandContext, Task> timeoutCallback = null)
        {
            if (string.IsNullOrEmpty(text) && embed == null)
            {
                throw new ArgumentException($"Either {nameof(text)} or {nameof(embed)} needs to be set.");
            }

            SingleUsePerUser = singleUsePerUser;
            ExpiresAfterUse = expiresAfterUse;
            Text = text ?? "";
            Embed = embed;
            Timeout = timeout;
            TimeoutCallback = timeoutCallback;
            Callbacks = new List<ReactionCallbackItem>();
        }

        public ICollection<ReactionCallbackItem> Callbacks { get; private set; }
        public bool ExpiresAfterUse { get; }
        public bool SingleUsePerUser { get; }
        public List<ulong> ReactorIDs { get; } = new List<ulong>();
        public string Text { get; }
        public Embed Embed { get; }
        public TimeSpan? Timeout { get; }
        public Func<SocketCommandContext, Task> TimeoutCallback { get; }

        public ReactionCallbackData AddCallbacks(IEnumerable<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)> callbacks)
        {
            foreach (var callback in callbacks)
            {
                Callbacks.Add(new ReactionCallbackItem(callback.Item1, callback.Item2));
            }

            return this;
        }

        public ReactionCallbackData SetCallbacks(IEnumerable<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)> callbacks)
        {
            Callbacks = callbacks.Select(x => new ReactionCallbackItem(x.Item1, x.Item2)).ToList();
            return this;
        }

        public ReactionCallbackData AddCallBack(IEmote reaction, Func<SocketCommandContext, SocketReaction, Task> callback)
        {
            Callbacks.Add(new ReactionCallbackItem(reaction, callback));
            return this;
        }

        public ReactionCallbackData WithCallback(IEmote reaction, Func<SocketCommandContext, SocketReaction, Task> callback)
        {
            var item = new ReactionCallbackItem(reaction, callback);
            Callbacks.Add(item);
            return this;
        }
    }
}