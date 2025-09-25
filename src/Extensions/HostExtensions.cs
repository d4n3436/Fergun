using System.IO;
using System.Linq;
using Fergun.Data;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fergun.Extensions;

/// <summary>
/// Contains extension methods for <see cref="IHost"/>.
/// </summary>
public static class HostExtensions
{
    /// <summary>
    /// Applies all pending migrations. The database will be created if it does not already exist.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The host.</returns>
    public static IHost ApplyMigrations(this IHost host)
    {
        Directory.CreateDirectory("data");

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FergunContext>();
        int pendingMigrations = db.Database.GetPendingMigrations().Count();

        if (pendingMigrations > 0)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            db.Database.Migrate();
            logger.LogInformation("Applied {Migrations}", "pending database migration".ToQuantity(pendingMigrations));
        }

        return host;
    }
}