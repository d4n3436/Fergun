using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a dictionary definition.
/// </summary>
public interface IDictionaryDefinition
{
    /// <summary>
    /// Gets the order of this definition.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets the content that is displayed before the definition.
    /// </summary>
    string PredefinitionContent { get; }

    /// <summary>
    /// Gets the content that is displayed after the definition.
    /// </summary>
    string PostdefinitionContent { get; }

    /// <summary>
    /// Gets the definition itself.
    /// </summary>
    string Definition { get; }

    /// <summary>
    /// Gets the sub-definitions.
    /// </summary>
    IReadOnlyList<IDictionaryDefinition> Subdefinitions { get; }
}