using System.Threading;
using System.Threading.Tasks;
using Discord;
using Fergun.Extensions;
using Fergun.Interactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fergun.Services;

/// <summary>
/// Hosts a logger for <see cref="InteractiveService"/>
/// </summary>
public class InteractiveServiceLoggerHost : IHostedService
{
    private readonly InteractiveService _interactive;
    private readonly ILogger<InteractiveService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveServiceLoggerHost"/> class.
    /// </summary>
    /// <param name="interactive">The <see cref="InteractiveService"/>.</param>
    /// <param name="logger">The logger.</param>
    public InteractiveServiceLoggerHost(InteractiveService interactive, ILogger<InteractiveService> logger)
    {
        _interactive = interactive;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _interactive.Log += Log;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _interactive.Log -= Log;
        return Task.CompletedTask;
    }

    private Task Log(LogMessage message)
    {
        _logger.Log(message.Severity.ToLogLevel(), new EventId(0, message.Source), message.Exception, "{ErrorMessage}", message.Message);
        return Task.CompletedTask;
    }
}