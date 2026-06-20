namespace Workes.SaveSystem
{
    /// <summary>
    /// Describes the outcome of a try-load operation.
    /// </summary>
    public enum SaveLoadStatus
    {
        /// <summary>
        /// The save or backup loaded successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// No save or backup folder was found for the requested identity.
        /// </summary>
        NotFound = 1,

        /// <summary>
        /// The backup system is disabled and a backup load was requested.
        /// </summary>
        BackupSystemDisabled = 2,

        /// <summary>
        /// The load request was invalid, such as a null identity or invalid backup slot number.
        /// </summary>
        InvalidRequest = 3,

        /// <summary>
        /// Provider registrations have not been validated.
        /// </summary>
        RegistrationsNotValidated = 4,

        /// <summary>
        /// A required provider save file was missing.
        /// </summary>
        MissingProviderFile = 5,

        /// <summary>
        /// Saved data could not be migrated to the registered provider schema version.
        /// </summary>
        MigrationFailed = 6,

        /// <summary>
        /// Recovery from an incomplete save operation failed.
        /// </summary>
        RecoveryFailed = 7,

        /// <summary>
        /// Saved data or metadata was present but invalid or unreadable.
        /// </summary>
        CorruptData = 8,

        /// <summary>
        /// Loading failed for a reason that does not fit a more specific status.
        /// </summary>
        LoadFailed = 9
    }
}
