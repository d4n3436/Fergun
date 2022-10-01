namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a topic that is under development.
/// </summary>
public interface IWolframAlphaFutureTopic
{
    /// <summary>
    /// Gets the topic.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Gets the message.
    /// </summary>
    string Message { get; }
}