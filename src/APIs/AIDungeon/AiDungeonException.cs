using System;

namespace Fergun.APIs.AIDungeon
{
    public class AiDungeonException : Exception
    {
        public AiDungeonException()
        {
        }

        public AiDungeonException(string message)
            : base(message)
        {
        }

        public AiDungeonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}