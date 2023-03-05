namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a dictionary client.
/// </summary>
public interface IDictionaryClient
{
    /// <summary>
    /// Searches for definitions.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only list of matching definitions.</returns>
    Task<IReadOnlyList<IDictionaryWord>> GetSearchResultsAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the definitions of a word.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the response.</returns>
    Task<IDictionaryResponse> GetDefinitionsAsync(string word, CancellationToken cancellationToken = default);
}