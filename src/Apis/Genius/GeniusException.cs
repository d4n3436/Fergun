using System;

namespace Fergun.Apis.Genius;

/// <summary>
/// The exception that is thrown when <see cref="GeniusClient"/> fails to scrape a song.
/// </summary>
public class GeniusException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeniusException"/> class.
    /// </summary>
    public GeniusException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeniusException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public GeniusException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeniusException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public GeniusException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}