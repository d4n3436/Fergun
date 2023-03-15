using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represents an Urban Dictionary API.
/// </summary>
public interface IUrbanDictionary
{
    /// <summary>
    /// Gets definitions for a term.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. The result contains a read-only collection of definitions.</returns>
    Task<IReadOnlyList<UrbanDefinition>> GetDefinitionsAsync(string term, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets random definitions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of random definitions.</returns>
    Task<IReadOnlyList<UrbanDefinition>> GetRandomDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a definition by its ID.
    /// </summary>
    /// <param name="id">The ID of the definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the definition, or <c>null</c> if not found.</returns>
    Task<UrbanDefinition?> GetDefinitionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the words of the day.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of definitions.</returns>
    Task<IReadOnlyList<UrbanDefinition>> GetWordsOfTheDayAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets autocomplete results for a term.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of suggested terms.</returns>
    Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string term, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets autocomplete results for a term. The results contain the term and a preview definition.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of suggested terms.</returns>
    Task<IReadOnlyList<UrbanAutocompleteResult>> GetAutocompleteResultsExtraAsync(string term, CancellationToken cancellationToken = default);
}