using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Structured result returned by non-mutating save validation operations.
    /// </summary>
    public sealed class SaveValidationResult
    {
        private SaveValidationResult(
            SaveLoadStatus status,
            string message,
            Exception? exception,
            SaveMetadataInfo? metadata)
        {
            Status = status;
            Message = message;
            Exception = exception;
            Metadata = metadata;
        }

        /// <summary>
        /// Gets whether the requested save or backup can be loaded by the current manager configuration.
        /// </summary>
        public bool IsValid => Status == SaveLoadStatus.Success;

        /// <summary>
        /// Gets the validation outcome status.
        /// </summary>
        public SaveLoadStatus Status { get; }

        /// <summary>
        /// Gets a short message describing the outcome.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception captured from the strict validation path when validation failed because of an error.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets core save metadata when validation succeeded; otherwise null.
        /// </summary>
        public SaveMetadataInfo? Metadata { get; }

        internal static SaveValidationResult Success(SaveMetadataInfo metadata)
        {
            return new SaveValidationResult(SaveLoadStatus.Success, string.Empty, null, metadata);
        }

        internal static SaveValidationResult NotFound(string message)
        {
            return new SaveValidationResult(SaveLoadStatus.NotFound, message, null, null);
        }

        internal static SaveValidationResult BackupSystemDisabled(string message)
        {
            return new SaveValidationResult(SaveLoadStatus.BackupSystemDisabled, message, null, null);
        }

        internal static SaveValidationResult Failure(SaveLoadStatus status, Exception exception)
        {
            return new SaveValidationResult(status, exception.Message, exception, null);
        }
    }
}
