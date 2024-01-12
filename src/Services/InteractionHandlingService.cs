using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Converters;
using Fergun.Data;
using Fergun.Data.Models;
using Fergun.Extensions;
using Fergun.Modules;
using GTranslate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Fergun.Services;

public sealed class InteractionHandlingService : IHostedService, IDisposable
{
    private readonly DiscordShardedClient _shardedClient;
    private readonly InteractionService _interactionService;
    private readonly FergunLocalizationManager _localizationManager;
    private readonly ILogger<InteractionHandlingService> _logger;
    private readonly IServiceProvider _services;
    private readonly ulong _testingGuildId;
    private readonly ulong _ownerCommandsGuildId;
    private readonly SemaphoreSlim _cmdStatsSemaphore = new(1, 1);
    private bool _disposed;

    public InteractionHandlingService(DiscordShardedClient client, InteractionService interactionService, FergunLocalizationManager localizationManager,
        ILogger<InteractionHandlingService> logger, IServiceProvider services, IOptions<StartupOptions> options)
    {
        _shardedClient = client;
        _interactionService = interactionService;
        _localizationManager = localizationManager;
        _logger = logger;
        _services = services;
        _testingGuildId = options.Value.TestingGuildId;
        _ownerCommandsGuildId = options.Value.OwnerCommandsGuildId;
        _interactionService.LocalizationManager = _localizationManager; // Should be set while configuring the services, but it's not possible
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _interactionService.SlashCommandExecuted += SlashCommandExecutedAsync;
        _interactionService.ContextCommandExecuted += ContextCommandExecutedAsync;
        _interactionService.ComponentCommandExecuted += ComponentCommandExecutedAsync;
        _interactionService.AutocompleteHandlerExecuted += AutocompleteHandlerExecutedAsync;
        _shardedClient.InteractionCreated += InteractionCreatedAsync;

        _interactionService.AddTypeConverter<System.Drawing.Color>(new ColorConverter());
        _interactionService.AddTypeConverter<MicrosoftVoice>(new MicrosoftVoiceConverter());

        await using var scope = _services.CreateAsyncScope();
        var modules = (await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), scope.ServiceProvider)).ToArray();
        _localizationManager.AddModules(modules);

        _logger.LogDebug("Added {Modules} command modules ({Commands} commands)", modules.Length,
            modules.Sum(x => x.ContextCommands.Count) + modules.Sum(x => x.SlashCommands.Count));

        _shardedClient.ShardReady += ReadyAsync;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _shardedClient.InteractionCreated -= InteractionCreatedAsync;
        _shardedClient.ShardReady -= ReadyAsync;

        return Task.CompletedTask;
    }

    public Task ReadyAsync(DiscordSocketClient client)
    {
        if (_shardedClient.Shards.All(x => x.ConnectionState == ConnectionState.Connected))
        {
            _shardedClient.ShardReady -= ReadyAsync;
            _ = Task.Run(async () =>
            {
                try
                {
                    await ReadyAsync();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "The ready handler has thrown an exception.");
                }
            });
        }

        return Task.CompletedTask;
    }

    public async Task ReadyAsync()
    {
        var modules = _interactionService.Modules.Where(x => x.Name is not nameof(OwnerModule) and not nameof(BlacklistModule));
        var ownerModules = _interactionService.Modules
            .Where(x => x.Name is nameof(OwnerModule) or nameof(BlacklistModule))
            .ToArray();

        if (_testingGuildId == 0)
        {
            _logger.LogInformation("Registering commands globally");
            await _interactionService.AddModulesGloballyAsync(true, modules.ToArray());

            if (_ownerCommandsGuildId != 0)
            {
                _logger.LogInformation("Registering owner commands to guild {GuildId}", _ownerCommandsGuildId);
                await _interactionService.AddModulesToGuildAsync(_ownerCommandsGuildId, true, ownerModules);
            }
        }
        else
        {
            _logger.LogInformation("Registering commands to guild {GuildId}", _testingGuildId);

            if (_testingGuildId == _ownerCommandsGuildId)
            {
                await _interactionService.RegisterCommandsToGuildAsync(_testingGuildId);
            }
            else
            {
                await _interactionService.AddModulesToGuildAsync(_testingGuildId, true, modules.ToArray());

                if (_ownerCommandsGuildId != 0)
                {
                    _logger.LogInformation("Registering owner commands to guild {GuildId}", _ownerCommandsGuildId);
                    await _interactionService.AddModulesToGuildAsync(_ownerCommandsGuildId, true, ownerModules);
                }
            }
        }
    }

    /// <summary>
    /// Disposes the <see cref="SemaphoreSlim"/> used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _cmdStatsSemaphore.Dispose();
        _disposed = true;
    }

    private Task InteractionCreatedAsync(SocketInteraction interaction)
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
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FergunContext>();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == interaction.User.Id);

        switch (user?.BlacklistStatus)
        {
            case BlacklistStatus.Blacklisted when interaction.Type is InteractionType.ApplicationCommand or InteractionType.MessageComponent:
            {
                _logger.LogInformation("Blacklisted user {User} ({UserId}) tried to execute an interaction.", interaction.User, interaction.User.Id);

                var localizer = _services.GetRequiredService<IFergunLocalizer<SharedResource>>();
                localizer.CurrentCulture = CultureInfo.GetCultureInfo(interaction.GetLanguageCode());

                string description = string.IsNullOrWhiteSpace(user.BlacklistReason)
                    ? localizer["Blacklisted"]
                    : localizer["BlacklistedWithReason", user.BlacklistReason];

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

    private Task SlashCommandExecutedAsync(SlashCommandInfo slashCommand, IInteractionContext context, IResult result)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleCommandExecutedAsync(slashCommand.ToString(), slashCommand.CommandType, context, result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The command-executed handler has thrown an exception.");
            }
        });

        return Task.CompletedTask;
    }

    private Task ContextCommandExecutedAsync(ContextCommandInfo contextCommand, IInteractionContext context, IResult result)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await HandleCommandExecutedAsync(contextCommand.Name, contextCommand.CommandType, context, result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The command-executed handler has thrown an exception.");
            }
        });

        return Task.CompletedTask;
    }

    private Task ComponentCommandExecutedAsync(ComponentCommandInfo? componentCommand, IInteractionContext context, IResult result)
    {
        if (componentCommand is null)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                await HandleCommandExecutedAsync(componentCommand.Name, null, context, result);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The command-executed handler has thrown an exception.");
            }
        });

        return Task.CompletedTask;
    }

    private Task AutocompleteHandlerExecutedAsync(IAutocompleteHandler autocompleteCommand, IInteractionContext context, IResult result)
    {
        var interaction = (IAutocompleteInteraction)context.Interaction;
        string subCommands = string.Join(" ", interaction.Data.Options
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

            if (exception is TimeoutRejectedException)
            {
                _logger.LogWarning("The autocomplete handler of command \"{Name}\" for {User} ({Id}) in {Context} was canceled because it exceeded the timeout.",
                    name, context.User, context.User.Id, context.Display());
            }
            else
            {
                _logger.LogError(exception, "Failed to execute Autocomplete handler of command \"{Name}\" for {User} ({Id}) in {Context} due to an exception.",
                    name, context.User, context.User.Id, context.Display());
            }
        }
        else
        {
            _logger.LogWarning("Failed to execute Autocomplete handler of command  \"{Name}\" for {User} ({Id}) in {Context}. Reason: {Reason}",
                name, context.User, context.User.Id, context.Display(), result.ErrorReason);
        }

        return Task.CompletedTask;
    }

    private async Task HandleCommandExecutedAsync(string commandName, ApplicationCommandType? type, IInteractionContext context, IResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (type is not null)
        {
            await _cmdStatsSemaphore.WaitAsync();

            try
            {
                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<FergunContext>();
                var dbCommand = await db.CommandStats.FirstOrDefaultAsync(x => x.Name == commandName);

                if (dbCommand is null)
                {
                    dbCommand = new Command { Name = commandName };
                    await db.AddAsync(dbCommand);
                }

                dbCommand.UsageCount++;

                await db.SaveChangesAsync();
            }
            finally
            {
                _cmdStatsSemaphore.Release();
            }
        }
        
        string commandType = type?.ToString() ?? "Component";

        if (result.IsSuccess)
        {
            _logger.LogInformation("Executed {Type} Command \"{Command}\" for {User} ({Id}) in {Context}",
                commandType, commandName, context.User, context.User.Id, context.Display());

            return;
        }

        if (result is IFergunResult { IsSilent: true })
            return;

        string message = result.ErrorReason;
        string? englishMessage = ((result as IFergunResult)?.LocalizedErrorReason as DualLocalizedString)?.EnglishValue;
        bool ephemeral = (result as IFergunResult)?.IsEphemeral ?? true;
        var interaction = (result as IFergunResult)?.Interaction ?? context.Interaction;

        if (result.Error == InteractionCommandError.Exception)
        {
            var exception = ((ExecuteResult)result).Exception;

            _logger.LogError(exception, "Failed to execute {Type} Command \"{Command}\" for {User} ({Id}) in {Context} due to an exception.",
                commandType, commandName, context.User, context.User.Id, context.Display());

            var localizer = _services.GetRequiredService<IFergunLocalizer<SharedResource>>();
            localizer.CurrentCulture = CultureInfo.GetCultureInfo(context.Interaction.GetLanguageCode());
            message = $"{localizer["AnErrorOccurred"]}\n\n{localizer["ErrorMessage", $"```{exception.Message}```"]}";
        }
        else if (result.Error == InteractionCommandError.Unsuccessful)
        {
            _logger.LogInformation("Unsuccessful execution of {Type} Command \"{Command}\" for {User} ({Id}) in {Context}. Reason: {Reason}",
                commandType, commandName, context.User, context.User.Id, context.Display(), englishMessage ?? message);
        }
        else
        {
            _logger.LogWarning("Failed to execute {Type} Command \"{Command}\" for {User} ({Id}) in {Context}. Reason: {Reason}",
                commandType, commandName, context.User, context.User.Id, context.Display(), englishMessage ?? message);
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