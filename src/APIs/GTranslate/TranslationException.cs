using System;
using System.Runtime.Serialization;

namespace Fergun.APIs.GTranslate
{
    /// <summary>
    /// The exception that is thrown when an error occurs during the translation process.
    /// </summary>
    [Serializable]
    public class TranslationException : Exception
    {
        public TranslationException()
        {
        }

        public TranslationException(string message) : base(message)
        {
        }

        public TranslationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TranslationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
