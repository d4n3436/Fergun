using System.Globalization;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Converters;
using Fergun.Data;
using Fergun.Data.Models;
using Fergun.Extensions;
using GTranslate;
using Microsoft.EntityFrameworkCore;
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
        _interactionService.SlashCommandExecuted += SlashCommandExecuted;
        _interactionService.ContextCommandExecuted += ContextCommandExecuted;
        _interactionService.AutocompleteHandlerExecuted += AutocompleteHandlerExecuted;
        _shardedClient.InteractionCreated += InteractionCreated;

        _interactionService.AddTypeConverter<System.Drawing.Color>(new ColorConverter());
        _interactionService.AddTypeConverter<MicrosoftVoice>(new MicrosoftVoiceConverter());
        var modules = (await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services)).ToArray();
        _logger.LogDebug("Added {Modules} command modules ({Commands} commands)", modules.Length,
            modules.Sum(x => x.ContextCommands.Count) + modules.Sum(x => x.SlashCommands.Count));

        _shardedClient.ShardReady += ReadyAsync;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _shardedClient.InteractionCreated -= InteractionCreated;
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

    private Task InteractionCreated(SocketInteraction interaction)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleInteractionAsync(interaction);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The interaction handler has thrown an exception.");
            }
        });

        return Task.CompletedTask;
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var db = _services.GetRequiredService<FergunContext>();
        
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == interaction.User.Id);
        
        switch (user?.BlacklistStatus)
        {
            case BlacklistStatus.Blacklisted when interaction.Type is InteractionType.ApplicationCommand or InteractionType.MessageComponent:
            {
                _logger.LogInformation("Blacklisted user {User} ({UserId}) tried to execute an interaction.", interaction.User, interaction.User.Id);
                
                var localizer = _services.GetRequiredService<IFergunLocalizer<SharedResource>>();
                localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

                string description = string.IsNullOrWhiteSpace(user.BlacklistReason)
                    ? localizer["You're blacklisted."]
                    : localizer["You're blacklisted with reason: {0}", user.BlacklistReason];
                    
                var builder = new EmbedBuilder()
                    .WithDescription($"❌ {description}")
                    .WithColor(Color.Orange);

                await interaction.RespondAsync(embed: builder.Build(), ephemeral: true);

                return;
            }
            case BlacklistStatus.ShadowBlacklisted:
                _logger.LogInformation("Shadow-blacklisted user {User} ({UserId}) tried to execute an interaction.", interaction.User, interaction.User.Id);
                return;
        }

        var context = new ShardedInteractionContext(_shardedClient, interaction);
        await _interactionService.ExecuteCommandAsync(context, _services);
    }

    private Task SlashCommandExecuted(SlashCommandInfo slashCommand, IInteractionContext context, IResult result)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleCommandExecutedAsync(slashCommand, context, result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The command-executed handler has thrown an exception.");
            }
        });

        return Task.CompletedTask;
    }

    private Task ContextCommandExecuted(ContextCommandInfo contextCommand, IInteractionContext context, IResult result)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleCommandExecutedAsync(contextCommand, context, result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The command-executed handler has thrown an exception.");
            }
        });
        
        return Task.CompletedTask;
    }

    private Task AutocompleteHandlerExecuted(IAutocompleteHandler autocompleteCommand, IInteractionContext context, IResult result)
    {
        var interaction = (IAutocompleteInteraction)context.Interaction;
        var subCommands = string.Join(" ", interaction.Data.Options
            .Where(x => x.Type == ApplicationCommandOptionType.SubCommand).Select(x => x.Name));

        if (!string.IsNullOrEmpty(subCommands))
        {
            subCommands = $" {subCommands}";
        }

        string name = $"{interaction.Data.CommandName}{subCommands} ({interaction.Data.Current.Name})";

        if (result.IsSuccess)
        {
            _logger.LogDebug("Executed Autocomplete handler of command \"{Name}\" for {User} ({Id}) in {Context}",
                name, context.User, context.User.Id, context.Display());
        }
        else if (result.Error == InteractionCommandError.Exception)
        {
            var exception = ((ExecuteResult)result).Exception;

            _logger.LogError(exception, "Failed to execute Autocomplete handler of command \"{Name}\" for {User} ({Id}) in {Context} due to an exception.",
                name, context.User, context.User.Id, context.Display());
        }
        else
        {
            _logger.LogWarning("Failed to execute Autocomplete handler of command  \"{Name}\" for {User} ({Id}) in {Context}. Reason: {Reason}",
                name, context.User, context.User.Id, context.Display(), result.ErrorReason);
        }
        

        return Task.CompletedTask;
    }

    private async Task HandleCommandExecutedAsync(IApplicationCommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess)
        {
            _logger.LogInformation("Executed {Type} Command \"{Command}\" for {User} ({Id}) in {Context}",
                command.CommandType, command, context.User, context.User.Id, context.Display());

            return;
        }
        
        if (result is FergunResult { IsSilent: true })
            return;

        string message = result.ErrorReason;
        bool ephemeral = (result as FergunResult)?.IsEphemeral ?? true;
        var interaction = (result as FergunResult)?.Interaction ?? context.Interaction;

        if (result.Error == InteractionCommandError.Exception)
        {
            var exception = ((ExecuteResult)result).Exception;

            _logger.LogError(exception, "Failed to execute {Type} Command \"{Command}\" for {User} ({Id}) in {Context} due to an exception.",
                command.CommandType, command, context.User, context.User.Id, context.Display());

            var localizer = _services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            message = $"{localizer["An error occurred."]}\n\n{localizer["Error message: {0}", $"```{exception.Message}```"]}";
        }
        else
        {
            _logger.LogWarning("Failed to execute {Type} Command \"{Command}\" for {User} ({Id}) in {Context}. Reason: {Reason}",
                command.CommandType, command, context.User, context.User.Id, context.Display(), message);
        }

        var embed = new EmbedBuilder()
            .WithDescription($"⚠ {message}")
            .WithColor(Color.Orange)
            .Build();

        if (context.Interaction.HasResponded)
        {
            await interaction.FollowupAsync(embed: embed, ephemeral: ephemeral);
        }
        else
        {
            await interaction.RespondAsync(embed: embed, ephemeral: ephemeral);
        }
    }
}