using System.Globalization;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fergun.Services;

public class InteractionHandlingService : IHostedService
{
    private readonly DiscordShardedClient _shardedClient;
    private readonly InteractionService _interactionService;
    private readonly ILogger<InteractionHandlingService> _logger;
    private readonly IServiceProvider _services;
    private readonly ulong _targetGuildId;

    public InteractionHandlingService(DiscordShardedClient client, InteractionService interactionService,
        ILogger<InteractionHandlingService> logger, IServiceProvider services, IConfiguration configuration)
    {
        _shardedClient = client;
        _interactionService = interactionService;
        _logger = logger;
        _services = services;
        _targetGuildId = configuration.Get<FergunConfig>().TargetGuildId;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _interactionService.Log += LogInteraction;
        _interactionService.SlashCommandExecuted += SlashCommandExecuted;
        _interactionService.ContextCommandExecuted += ContextMenuCommandExecuted;
        _shardedClient.InteractionCreated += HandleInteractionAsync;

        var modules = await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        _logger.LogDebug("Added {moduleCount} command modules", modules.Count());

        _shardedClient.ShardReady += ReadyAsync;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _interactionService.Log -= LogInteraction;
        _shardedClient.InteractionCreated -= HandleInteractionAsync;
        _shardedClient.ShardReady -= ReadyAsync;

        return Task.CompletedTask;
    }

    public async Task ReadyAsync(DiscordSocketClient client)
    {
        if (_shardedClient.Shards.All(x => x.ConnectionState == ConnectionState.Connected))
        {
            _shardedClient.ShardReady -= ReadyAsync;
            await ReadyAsync();
        }
    }

    public async Task ReadyAsync()
    {
        if (_targetGuildId == 0)
        {
            _logger.LogInformation("Registering commands globally");
            await _interactionService.RegisterCommandsGloballyAsync();
        }
        else
        {
            _logger.LogInformation("Registering commands to guild {guildId}", _targetGuildId);
            await _interactionService.RegisterCommandsToGuildAsync(_targetGuildId);
        }
    }

    public async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var context = new ShardedInteractionContext(_shardedClient, interaction);
        await _interactionService.ExecuteCommandAsync(context, _services);
    }

    private async Task SlashCommandExecuted(SlashCommandInfo slashCommand, IInteractionContext context, IResult result)
    {
        _logger.LogInformation("Executed slash command \"{name}\" for {username}#{discriminator} ({id}) in {context}",
            slashCommand.Name, context.User.Username, context.User.Discriminator, context.User.Id, context.Display());

        if (result.IsSuccess)
            return;

        await HandleInteractionErrorAsync(context, result);
    }

    private async Task ContextMenuCommandExecuted(ContextCommandInfo contextCommand, IInteractionContext context, IResult result)
    {
        _logger.LogInformation("Executed context menu command \"{name}\" for {username}#{discriminator} ({id}) in {context}",
            contextCommand.Name, context.User.Username, context.User.Discriminator, context.User.Id, context.Display());

        if (result.IsSuccess || result is FergunResult { IsSilent: true })
            return;

        await HandleInteractionErrorAsync(context, result);
    }

    private async Task HandleInteractionErrorAsync(IInteractionContext context, IResult result)
    {
        string message = result.ErrorReason;
        bool ephemeral = (result as FergunResult)?.IsEphemeral ?? true;
        var interaction = (result as FergunResult)?.Interaction ?? context.Interaction;

        if (result.Error == InteractionCommandError.Exception)
        {
            var localizer = _services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            message = $"{localizer["An error occurred."]}\n\n{localizer["Error message: {0}", $"```{((ExecuteResult)result).Exception.Message}```"]}";
        }

        if (context.Interaction.HasResponded)
        {
            await interaction.FollowupWarning($"⚠ {message}", ephemeral);
        }
        else
        {
            await interaction.RespondWarningAsync($"⚠ {message}", ephemeral);
        }
    }

    private Task LogInteraction(LogMessage log)
    {
#pragma warning disable CA2254 // Template should be a static expression
        _logger.Log(log.Severity.ToLogLevel(), new EventId(0, log.Source), log.Exception, log.Message);
#pragma warning restore CA2254 // Template should be a static expression
        return Task.CompletedTask;
    }
}