using System;
using System.IO;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Configuration options for a <see cref="SaveManager{TIdentity}"/> instance.
    /// </summary>
    /// <typeparam name="TIdentity">The type used to identify saves.</typeparam>
    public sealed class SaveSystemOptions<TIdentity> where TIdentity : ISaveIdentity
    {
        /// <summary>
        /// Gets the root directory path where all saves are stored.
        /// </summary>
        public string SaveRootPath { get; }

        /// <summary>
        /// Gets the serializer used to convert provider states to/from file formats.
        /// </summary>
        public ISaveSerializer Serializer { get; }

        /// <summary>
        /// Gets the suffix used for temporary folders during atomic save operations.
        /// </summary>
        public string TempFolderName { get; }

        /// <summary>
        /// Gets the function that resolves an identity to a save folder name.
        /// </summary>
        public Func<TIdentity, string> SaveNameResolver { get; }

        /// <summary>
        /// Gets the function that resolves a file context to a file name for provider save files.
        /// </summary>
        /// <remarks>
        /// Using <see cref="SaveFileContext.SchemaVersion"/> in the resolved file name is NOT RECOMMENDED.
        /// Schema version belongs in the save data envelope; including it in the file name prevents loading
        /// older saves when the provider's runtime schema version has increased (the loader would look for
        /// a different filename).
        /// </remarks>
        public Func<SaveFileContext, string> FileNameResolver { get; }

        /// <summary>
        /// Gets whether the backup system is enabled. When enabled, old saves are kept as numbered backups.
        /// </summary>
        public bool EnableBackupSystem { get; }

        /// <summary>
        /// Gets the maximum number of backups to keep when the backup system is enabled.
        /// Must be greater than 0 if <see cref="EnableBackupSystem"/> is true.
        /// </summary>
        public int BackupSystemMaxBackupCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveSystemOptions{TIdentity}"/> class.
        /// </summary>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="tempFolderName">The suffix used for temporary folders during atomic save operations.</param>
        /// <param name="saveNameResolver">The function that resolves an identity to a save folder name.</param>
        /// <param name="fileNameResolver">The function that resolves a file context to a file name. If null, uses <see cref="DefaultFileNameResolver"/>.</param>
        /// <param name="enableBackupSystem">Whether to enable the backup system. Defaults to false.</param>
        /// <param name="backupSystemMaxBackupCount">The maximum number of backups to keep. Must be greater than 0 if backups are enabled. Defaults to 0.</param>
        /// <exception cref="ArgumentException">Thrown when path values are invalid, or when backups are enabled but max backup count is 0 or less.</exception>
        /// <exception cref="ArgumentNullException">Thrown when serializer or resolver delegates are null.</exception>
        public SaveSystemOptions(
            string saveRootPath,
            ISaveSerializer serializer,
            string tempFolderName,
            Func<TIdentity, string> saveNameResolver,
            Func<SaveFileContext, string>? fileNameResolver,
            bool enableBackupSystem = false,
            int backupSystemMaxBackupCount = 0)
        {
            if (string.IsNullOrWhiteSpace(saveRootPath))
                throw new ArgumentException("Save root path cannot be null, empty, or whitespace.", nameof(saveRootPath));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            if (saveNameResolver == null)
                throw new ArgumentNullException(nameof(saveNameResolver));

            ValidatePathSegment(tempFolderName, nameof(tempFolderName));

            SaveRootPath = saveRootPath;
            Serializer = serializer;
            SaveNameResolver = saveNameResolver;
            TempFolderName = tempFolderName;
            FileNameResolver = fileNameResolver ?? DefaultFileNameResolver;
            EnableBackupSystem = enableBackupSystem;
            BackupSystemMaxBackupCount = backupSystemMaxBackupCount;
            if (enableBackupSystem && backupSystemMaxBackupCount <= 0)
                throw new ArgumentException("If the backup system is enabled, the max backup count must be greater than 0.");
        }

        /// <summary>
        /// Default file name resolver that uses only the save key: "{SaveKey}".
        /// Schema version is not included so that the same file is used across schema versions and
        /// migration can load older saves from disk. Using schema version in file names is NOT RECOMMENDED.
        /// </summary>
        /// <param name="context">The file context containing save key and schema version.</param>
        /// <returns>A file name string (save key only).</returns>
        public static string DefaultFileNameResolver(SaveFileContext context)
        {
            return context.SaveKey;
        }

        /// <summary>
        /// Returns the default temporary folder suffix: "_tmp".
        /// </summary>
        /// <returns>The default temporary folder suffix.</returns>
        public static string DefaultTempFolderName()
        {
            return "_tmp";
        }

        private static void ValidatePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Path segment cannot be null, empty, or whitespace.", parameterName);

            if (value == "." || value == "..")
                throw new ArgumentException("Path segment cannot be '.' or '..'.", parameterName);

            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Path segment cannot contain path or file-name separator characters.", parameterName);
        }
    }
}
