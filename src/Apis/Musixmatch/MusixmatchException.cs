﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// The exception that is thrown when a call to the Musixmatch API fails.
/// </summary>
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
    /// Initializes a new instance of the <see cref="MusixmatchException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="hint">The hint.</param>
    public MusixmatchException(string? message, string? hint)
        : base(message)
    {
        Hint = hint;
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
    /// Gets a hint from the API that describes the error.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    /// Throws a <see cref="MusixmatchException"/> with a message based on the provided parameters.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="path">The path.</param>
    /// <param name="hint">A hint from the API that describes the error.</param>
    /// <exception cref="MusixmatchException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Throw(HttpStatusCode statusCode, string? path, string? hint)
    {
        string message = hint switch
        {
            "renew" => $"Request failed due to an expired user token.{(path is null ? string.Empty : $" (Path: {path})")}",
            "captcha" => $"Musixmatch API returned a CAPTCHA. Try again later.{(path is null ? string.Empty : $" (Path: {path})")}",
            _ => $"The API returned a {(int)statusCode} ({statusCode}) status code.{(path is null ? string.Empty : $" (Path: {path})")}"
        };

        throw new MusixmatchException(message, hint);
    }
}