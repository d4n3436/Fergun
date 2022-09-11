using System.Runtime.Serialization;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// The exception that is thrown when <see cref="MusixmatchClient"/> fails to scrape a song.
/// </summary>
[Serializable]
public class MusixmatchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchException"/> class.
    /// </summary>
    public MusixmatchException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MusixmatchException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public MusixmatchException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchException"/> class with serialized data.
    /// </summary>
    /// <param name="serializationInfo">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="streamingContext">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
    protected MusixmatchException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        : base(serializationInfo, streamingContext)
    {
    }
}