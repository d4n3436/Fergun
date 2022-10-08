namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a WolframAlpha client.
/// </summary>
public interface IWolframAlphaClient
{
    /// <summary>
    /// Gets autocomplete results that matches <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The query input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only list containing the results.</returns>
    Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets results from WolframAlpha.
    /// </summary>
    /// <param name="input">The query input.</param>
    /// <param name="language">The language of the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns> <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the response from WolframAlpha.</returns>
    Task<IWolframAlphaResult> GetResultsAsync(string input, string language, CancellationToken cancellationToken = default);
}