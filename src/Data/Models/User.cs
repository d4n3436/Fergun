using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Discord;

namespace Fergun.Data.Models;

/// <summary>
/// Represents a database user.
/// </summary>
public class User : IEntity<ulong>
{
    /// <inheritdoc/>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the blacklist status.
    /// </summary>
    public BlacklistStatus BlacklistStatus { get; set; }

    /// <summary>
    /// Gets or sets the blacklist reason.
    /// </summary>
    [MaxLength(256)]
    public string? BlacklistReason { get; set; }
}