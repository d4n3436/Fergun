using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Discord.Addons.Interactive
{
    public class ReactionCallbackData
    {
        private ICollection<ReactionCallbackItem> items;

        public ReactionCallbackData(string text, Embed embed = null, bool expiresAfterUse = true, bool singleUsePerUser = true, TimeSpan? timeout = null, Func<SocketCommandContext, Task> timeoutCallback = null)
        {
            if (text == null && embed == null)
            {
                throw new Exception("Inline reaction must have message data");
            }

            SingleUsePerUser = singleUsePerUser;
            ExpiresAfterUse = expiresAfterUse;
            ReactorIDs = new List<ulong>();
            Text = text ?? "";
            Embed = embed;
            Timeout = timeout;
            TimeoutCallback = timeoutCallback;
            items = new List<ReactionCallbackItem>();
        }

        public IEnumerable<ReactionCallbackItem> Callbacks => items;
        public bool ExpiresAfterUse { get; }
        public bool SingleUsePerUser { get; }
        public List<ulong> ReactorIDs { get; }
        public string Text { get; }
        public Embed Embed { get; }
        public TimeSpan? Timeout { get; }
        public Func<SocketCommandContext, Task> TimeoutCallback { get; }

        public ReactionCallbackData AddCallbacks(IEnumerable<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)> callbacks)
        {
            foreach (var callback in callbacks)
            {
                items.Add(new ReactionCallbackItem(callback.Item1, callback.Item2));
            }

            return this;
        }

        public ReactionCallbackData SetCallbacks(IEnumerable<(IEmote, Func<SocketCommandContext, SocketReaction, Task>)> callbacks)
        {
            items = callbacks.Select(x => new ReactionCallbackItem(x.Item1, x.Item2)).ToList();
            return this;
        }

        public ReactionCallbackData AddCallBack(IEmote reaction, Func<SocketCommandContext, SocketReaction, Task> callback)
        {
            items.Add(new ReactionCallbackItem(reaction, callback));
            return this;
        }

        public ReactionCallbackData WithCallback(IEmote reaction, Func<SocketCommandContext, SocketReaction, Task> callback)
        {
            var item = new ReactionCallbackItem(reaction, callback);
            items.Add(item);
            return this;
        }
    }
}