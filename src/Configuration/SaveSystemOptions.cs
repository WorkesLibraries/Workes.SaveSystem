using System;
using System.IO;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides convenience factories for common <see cref="SaveSystemOptions{TIdentity}"/> construction paths.
    /// </summary>
    public static class SaveSystemOptions
    {
        /// <summary>
        /// Creates options for string save identities using default temp-folder and provider-file naming.
        /// </summary>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="tempFolderName">Optional temp folder suffix. If null, uses the default temp folder name.</param>
        /// <param name="fileNameResolver">Optional provider file-name resolver. If null, uses the default file-name resolver.</param>
        /// <param name="missingProviderFileBehavior">How loads behave when a registered persisted provider file is missing.</param>
        /// <param name="warningSink">Optional callback that receives save-system warning messages.</param>
        /// <returns>Options configured for string save identities.</returns>
        public static SaveSystemOptions<string> Create(
            string saveRootPath,
            ISaveSerializer serializer,
            string? tempFolderName = null,
            Func<SaveFileContext, string>? fileNameResolver = null,
            MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
            Action<string>? warningSink = null)
        {
            return Create<string>(
                saveRootPath,
                serializer,
                savePathResolver: identity => identity,
                tempFolderName: tempFolderName,
                fileNameResolver: fileNameResolver,
                missingProviderFileBehavior: missingProviderFileBehavior,
                warningSink: warningSink);
        }

        /// <summary>
        /// Creates options for custom save identities using default temp-folder and provider-file naming.
        /// </summary>
        /// <typeparam name="TIdentity">The type used to identify saves.</typeparam>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="savePathResolver">The function that resolves an identity to a safe relative save path.</param>
        /// <param name="tempFolderName">Optional temp folder suffix. If null, uses the default temp folder name.</param>
        /// <param name="fileNameResolver">Optional provider file-name resolver. If null, uses the default file-name resolver.</param>
        /// <param name="missingProviderFileBehavior">How loads behave when a registered persisted provider file is missing.</param>
        /// <param name="warningSink">Optional callback that receives save-system warning messages.</param>
        /// <returns>Options configured for <typeparamref name="TIdentity"/> save identities.</returns>
        public static SaveSystemOptions<TIdentity> Create<TIdentity>(
            string saveRootPath,
            ISaveSerializer serializer,
            Func<TIdentity, string> savePathResolver,
            string? tempFolderName = null,
            Func<SaveFileContext, string>? fileNameResolver = null,
            MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
            Action<string>? warningSink = null)
        {
            return new SaveSystemOptions<TIdentity>(
                saveRootPath: saveRootPath,
                serializer: serializer,
                tempFolderName: tempFolderName ?? SaveSystemOptions<TIdentity>.DefaultTempFolderName(),
                savePathResolver: savePathResolver,
                fileNameResolver: fileNameResolver,
                missingProviderFileBehavior: missingProviderFileBehavior,
                warningSink: warningSink);
        }

        /// <summary>
        /// Creates options for string save identities with backups enabled.
        /// </summary>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="backupSystemMaxBackupCount">The maximum number of backups to keep. Must be greater than 0.</param>
        /// <param name="tempFolderName">Optional temp folder suffix. If null, uses the default temp folder name.</param>
        /// <param name="fileNameResolver">Optional provider file-name resolver. If null, uses the default file-name resolver.</param>
        /// <param name="missingProviderFileBehavior">How loads behave when a registered persisted provider file is missing.</param>
        /// <param name="warningSink">Optional callback that receives save-system warning messages.</param>
        /// <returns>Options configured for string save identities with backups enabled.</returns>
        public static SaveSystemOptions<string> CreateWithBackups(
            string saveRootPath,
            ISaveSerializer serializer,
            int backupSystemMaxBackupCount,
            string? tempFolderName = null,
            Func<SaveFileContext, string>? fileNameResolver = null,
            MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
            Action<string>? warningSink = null)
        {
            return CreateWithBackups<string>(
                saveRootPath,
                serializer,
                savePathResolver: identity => identity,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount,
                tempFolderName: tempFolderName,
                fileNameResolver: fileNameResolver,
                missingProviderFileBehavior: missingProviderFileBehavior,
                warningSink: warningSink);
        }

        /// <summary>
        /// Creates options for custom save identities with backups enabled.
        /// </summary>
        /// <typeparam name="TIdentity">The type used to identify saves.</typeparam>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="savePathResolver">The function that resolves an identity to a safe relative save path.</param>
        /// <param name="backupSystemMaxBackupCount">The maximum number of backups to keep. Must be greater than 0.</param>
        /// <param name="tempFolderName">Optional temp folder suffix. If null, uses the default temp folder name.</param>
        /// <param name="fileNameResolver">Optional provider file-name resolver. If null, uses the default file-name resolver.</param>
        /// <param name="missingProviderFileBehavior">How loads behave when a registered persisted provider file is missing.</param>
        /// <param name="warningSink">Optional callback that receives save-system warning messages.</param>
        /// <returns>Options configured for <typeparamref name="TIdentity"/> save identities with backups enabled.</returns>
        public static SaveSystemOptions<TIdentity> CreateWithBackups<TIdentity>(
            string saveRootPath,
            ISaveSerializer serializer,
            Func<TIdentity, string> savePathResolver,
            int backupSystemMaxBackupCount,
            string? tempFolderName = null,
            Func<SaveFileContext, string>? fileNameResolver = null,
            MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
            Action<string>? warningSink = null)
        {
            return new SaveSystemOptions<TIdentity>(
                saveRootPath: saveRootPath,
                serializer: serializer,
                tempFolderName: tempFolderName ?? SaveSystemOptions<TIdentity>.DefaultTempFolderName(),
                savePathResolver: savePathResolver,
                fileNameResolver: fileNameResolver,
                enableBackupSystem: true,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount,
                missingProviderFileBehavior: missingProviderFileBehavior,
                warningSink: warningSink);
        }
    }

    /// <summary>
    /// Configuration options for a <see cref="SaveManager{TIdentity}"/> instance.
    /// </summary>
    /// <typeparam name="TIdentity">The type used to identify saves.</typeparam>
    public sealed class SaveSystemOptions<TIdentity>
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
        /// Gets the function that resolves an identity to a safe relative save path.
        /// </summary>
        public Func<TIdentity, string> SavePathResolver { get; }

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
        /// Gets how loads behave when a registered persisted provider file is missing from a legacy save folder
        /// without a provider manifest. Manifest-backed saves still fail when a manifest-present provider file is missing.
        /// </summary>
        public MissingProviderFileBehavior MissingProviderFileBehavior { get; }

        /// <summary>
        /// Gets the optional callback that receives save-system warning messages.
        /// </summary>
        public Action<string>? WarningSink { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveSystemOptions{TIdentity}"/> class.
        /// </summary>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <param name="serializer">The serializer used to convert provider states to/from file formats.</param>
        /// <param name="tempFolderName">The suffix used for temporary folders during atomic save operations.</param>
        /// <param name="savePathResolver">The function that resolves an identity to a safe relative save path.</param>
        /// <param name="fileNameResolver">The function that resolves a file context to a file name. If null, uses <see cref="DefaultFileNameResolver"/>.</param>
        /// <param name="enableBackupSystem">Whether to enable the backup system. Defaults to false.</param>
        /// <param name="backupSystemMaxBackupCount">The maximum number of backups to keep. Must be greater than 0 if backups are enabled. Defaults to 0.</param>
        /// <param name="missingProviderFileBehavior">How loads behave when a registered persisted provider file is missing from a legacy save without a provider manifest. Defaults to <see cref="MissingProviderFileBehavior.Throw"/>.</param>
        /// <param name="warningSink">Optional callback that receives save-system warning messages. Defaults to null.</param>
        /// <exception cref="ArgumentException">Thrown when path values are invalid, or when backups are enabled but max backup count is 0 or less.</exception>
        /// <exception cref="ArgumentNullException">Thrown when serializer or resolver delegates are null.</exception>
        public SaveSystemOptions(
            string saveRootPath,
            ISaveSerializer serializer,
            string tempFolderName,
            Func<TIdentity, string> savePathResolver,
            Func<SaveFileContext, string>? fileNameResolver,
            bool enableBackupSystem = false,
            int backupSystemMaxBackupCount = 0,
            MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
            Action<string>? warningSink = null)
        {
            if (string.IsNullOrWhiteSpace(saveRootPath))
                throw new ArgumentException("Save root path cannot be null, empty, or whitespace.", nameof(saveRootPath));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            if (savePathResolver == null)
                throw new ArgumentNullException(nameof(savePathResolver));

            ValidatePathSegment(tempFolderName, nameof(tempFolderName));

            SaveRootPath = saveRootPath;
            Serializer = serializer;
            SavePathResolver = savePathResolver;
            TempFolderName = tempFolderName;
            FileNameResolver = fileNameResolver ?? DefaultFileNameResolver;
            EnableBackupSystem = enableBackupSystem;
            BackupSystemMaxBackupCount = backupSystemMaxBackupCount;
            MissingProviderFileBehavior = missingProviderFileBehavior;
            WarningSink = warningSink;
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
