namespace Fergun.Tests;

/// <summary>
/// Shared magic strings used by mocked APIs and module tests.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// An image URL whose mocked API returns text/results.
    /// </summary>
    public const string TextImageUrl = "https://example.com/image.png";

    /// <summary>
    /// An image URL whose mocked API returns an empty result.
    /// </summary>
    public const string EmptyImageUrl = "https://example.com/empty.png";

    /// <summary>
    /// An image URL whose mocked API throws the API's exception type.
    /// </summary>
    public const string ErrorImageUrl = "https://example.com/error";

    /// <summary>
    /// An image URL pointing at an unsupported file, used to exercise invalid-input handling.
    /// </summary>
    public const string InvalidImageUrl = "https://example.com/file.bin";
}