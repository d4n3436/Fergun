using System;

namespace Fergun.Apis.Yandex;

/// <summary>
/// The exception that is thrown when Yandex Image search fails to retrieve the results of an operation.
/// </summary>
public sealed class YandexException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YandexException"/> class.
    /// </summary>
    public YandexException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YandexException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public YandexException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YandexException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public YandexException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}