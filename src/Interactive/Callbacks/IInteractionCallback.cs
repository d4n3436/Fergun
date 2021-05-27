using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Interactive
{
    public interface IInteractionCallback
    {
        ICriterion<SocketInteraction> Criterion { get; }

        TimeSpan? Timeout { get; }

        SocketCommandContext Context { get; }

        Task<bool> HandleCallbackAsync(SocketInteraction interaction, string button);
    }
}