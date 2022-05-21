using GTranslate.Translators;

namespace Fergun;

/// <summary>
/// Provides methods to modify of the order of translators.
/// </summary>
public interface IFergunTranslator : ITranslator
{
    /// <summary>
    /// Switches to the next translator.
    /// </summary>
    void Next();

    /// <summary>
    /// Randomizes the order of the translators.
    /// </summary>
    void Randomize();
}