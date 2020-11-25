using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Modules
{
    public sealed class EvaluationEnvironment
    {
        public ShardedCommandContext Context { get; }
        public SocketUserMessage Message => Context.Message;
        public ISocketMessageChannel Channel => Context.Channel;
        public SocketGuild Guild => Context.Guild;
        public SocketUser User => Context.User;
        public DiscordShardedClient Client => Context.Client;

        public EvaluationEnvironment(ShardedCommandContext context)
        {
            Context = context;
        }
    }
}