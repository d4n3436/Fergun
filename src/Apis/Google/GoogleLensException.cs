using System;

namespace Fergun.Apis.Google;

/// <summary>
/// The exception that is thrown when Google Lens fails to retrieve the results of an operation.
/// </summary>
public class GoogleLensException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLensException"/> class.
    /// </summary>
    public GoogleLensException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLensException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public GoogleLensException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLensException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public GoogleLensException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}