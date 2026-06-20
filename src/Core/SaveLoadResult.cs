using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Structured result returned by try-load operations.
    /// </summary>
    public sealed class SaveLoadResult
    {
        private SaveLoadResult(SaveLoadStatus status, string message, Exception? exception)
        {
            Status = status;
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Gets whether the requested save or backup loaded successfully.
        /// </summary>
        public bool Succeeded => Status == SaveLoadStatus.Success;

        /// <summary>
        /// Gets the load outcome status.
        /// </summary>
        public SaveLoadStatus Status { get; }

        /// <summary>
        /// Gets a short message describing the outcome.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception captured from the strict load path when loading failed because of an error.
        /// </summary>
        public Exception? Exception { get; }

        internal static SaveLoadResult Success()
        {
            return new SaveLoadResult(SaveLoadStatus.Success, string.Empty, null);
        }

        internal static SaveLoadResult NotFound(string message)
        {
            return new SaveLoadResult(SaveLoadStatus.NotFound, message, null);
        }

        internal static SaveLoadResult BackupSystemDisabled(string message)
        {
            return new SaveLoadResult(SaveLoadStatus.BackupSystemDisabled, message, null);
        }

        internal static SaveLoadResult Failure(SaveLoadStatus status, Exception exception)
        {
            return new SaveLoadResult(status, exception.Message, exception);
        }
    }
}
