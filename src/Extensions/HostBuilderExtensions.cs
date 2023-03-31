using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Fergun.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IHostBuilder"/>.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Replaces the default HTTP logging with a simpler one.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The host builder.</returns>
    public static IHostBuilder UseFergunRequestLogging(this IHostBuilder builder)
    {
        return builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, FergunLoggingHttpMessageHandlerBuilderFilter>();
        });
    }
}