using System;

namespace Workes.SaveSystem
{
    internal sealed class SaveLoadException : Exception
    {
        public SaveLoadException(SaveLoadStatus status, string message)
            : base(message)
        {
            Status = status;
        }

        public SaveLoadException(SaveLoadStatus status, string message, Exception innerException)
            : base(message, innerException)
        {
            Status = status;
        }

        public SaveLoadStatus Status { get; }

        public static InvalidOperationException Create(SaveLoadStatus status, string message)
        {
            return new InvalidOperationException(message, new SaveLoadException(status, message));
        }

        public static InvalidOperationException Create(SaveLoadStatus status, string message, Exception innerException)
        {
            return new InvalidOperationException(
                message,
                new SaveLoadException(status, message, innerException));
        }
    }
}
