using Fergun.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Fergun.Data;

/// <summary>
/// Represents the Fergun database context.
/// </summary>
public class FergunContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FergunContext"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public FergunContext(DbContextOptions<FergunContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Gets or sets the command stats.
    /// </summary>
    public DbSet<Command> CommandStats { get; set; } = null!;
}