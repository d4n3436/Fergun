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
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains an <see cref="IEnumerable{T}"/> containing the results.</returns>
    Task<IEnumerable<string>> GetAutocompleteResultsAsync(string input);

    /// <summary>
    /// Gets results from WolframAlpha.
    /// </summary>
    /// <param name="input">The query input.</param>
    /// <param name="language">The language of the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns> <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the response from WolframAlpha.</returns>
    Task<IWolframAlphaResult> GetResultsAsync(string input, string language, CancellationToken cancellationToken);
}