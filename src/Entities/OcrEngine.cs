using Fergun.Modules;

namespace Fergun;

/// <summary>
/// Specifies the OCR engines used in <see cref="OcrModule"/>.
/// </summary>
public enum OcrEngine
{
    /// <summary>
    /// Google.
    /// </summary>
    Google = 0,

    /// <summary>
    /// Bing.
    /// </summary>
    Bing = 1,

    /// <summary>
    /// Yandex.
    /// </summary>
    Yandex = 2
}