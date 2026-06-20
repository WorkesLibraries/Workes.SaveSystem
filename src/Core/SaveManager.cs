using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Manages saving and loading game state to/from disk. Provides a centralized system for
    /// registering save providers, capturing snapshots, and persisting data with optional backup support.
    /// </summary>
    /// <typeparam name="TIdentity">The type used to identify saves.</typeparam>
    public sealed class SaveManager<TIdentity>
    {
        private sealed class ProviderEntry
        {
            public ISaveProvider Provider = null!;
            public ISaveSchematic? Schematic;
            public Type StateType = null!;
            public Func<object> CaptureState = null!;
            public Action<object> RestoreState = null!;
        }

        private const string SaveMetadataFileName = "savemetadata.json";
        private const string BackupFolderName = "_backup";
        private const string ToDeleteFolderName = "_toDelete";

        private string GetMainFolderPath(string saveName) =>
            Path.Combine(_options.SaveRootPath, saveName);

        private string GetTempFolderPath(string saveName, string tempFolderSuffix) =>
            Path.Combine(_options.SaveRootPath, saveName + tempFolderSuffix);

        private string GetToDeleteFolderPath(string saveName) =>
            Path.Combine(_options.SaveRootPath, saveName + ToDeleteFolderName);

        private string GetMetadataFilePath(string folderPath) =>
            Path.Combine(folderPath, SaveMetadataFileName);

        private string GetProviderFilePath(string folderPath, string fileName) =>
            Path.Combine(folderPath, fileName);

        private string GetProviderFilePath(string folderPath, SaveFileContext context) =>
            Path.Combine(folderPath, _options.FileNameResolver(context) + _options.Serializer.FileExtension);

        private readonly Dictionary<string, ProviderEntry> _providers = new Dictionary<string, ProviderEntry>();

        private readonly SaveSystemOptions<TIdentity> _options;
        private readonly BackupManager _backupManager;
        private readonly MigrationEngine _migrationEngine;
        private readonly SaveSystemDiagnostics _diagnostics;
        private bool _registrationsValidated;

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveManager{TIdentity}"/> class with the specified options.
        /// </summary>
        /// <param name="options">Configuration options for the save system.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public SaveManager(SaveSystemOptions<TIdentity> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _options = options;
            _diagnostics = new SaveSystemDiagnostics(options.WarningSink);
            _backupManager = new BackupManager(
                options.SaveRootPath,
                options.EnableBackupSystem,
                options.BackupSystemMaxBackupCount,
                _diagnostics
            );
            _migrationEngine = new MigrationEngine(options.Serializer, _diagnostics);
        }

        /// <summary>
        /// Creates a default save manager instance using <see cref="string"/> for save identification.
        /// Uses the current user application data folder as the save root directory. This is intended as a
        /// convenience default for plain .NET applications; Unity, Godot, and other host engines should prefer
        /// <see cref="CreateDefault(ISaveSerializer, string)"/> and pass an engine-owned persistent data path.
        /// </summary>
        /// <param name="serializer">The serializer to use for saving and loading data.</param>
        /// <returns>A new string-identity save manager instance with default configuration.</returns>
        public static SaveManager<string> CreateDefault(ISaveSerializer serializer)
        {
            return CreateDefault(
                serializer,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Workes", "SaveSystem")
            );
        }

        /// <summary>
        /// Creates a default save manager instance using <see cref="string"/> for save identification
        /// and an explicit save root directory supplied by the host application or engine.
        /// </summary>
        /// <param name="serializer">The serializer to use for saving and loading data.</param>
        /// <param name="saveRootPath">The root directory path where all saves are stored.</param>
        /// <returns>A new string-identity save manager instance with default configuration.</returns>
        public static SaveManager<string> CreateDefault(ISaveSerializer serializer, string saveRootPath)
        {
            return new SaveManager<string>(
                SaveSystemOptions.Create(
                    saveRootPath: saveRootPath,
                    serializer: serializer)
            );
        }

        /// <summary>
        /// Registers a typed save provider for persistence. The serializer creates a schematic for the provider state type.
        /// </summary>
        /// <remarks>
        /// Use this overload for providers that should be written to disk. The provider key and schema version become
        /// part of the persisted save contract, so changing them can affect compatibility with existing saves.
        /// Call <see cref="ValidateRegistrations"/> after registration is complete and before disk save/load operations.
        /// </remarks>
        /// <typeparam name="TState">The state type the provider captures and restores (e.g. <c>PlayerState</c>).</typeparam>
        /// <param name="provider">The save provider to register. Must have a unique <see cref="ISaveProvider.SaveKey"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a provider with the same key is already registered.</exception>
        public void RegisterProvider<TState>(ISaveProvider<TState> provider)
        {
            ValidateProvider(provider);

            if (_providers.TryGetValue(provider.SaveKey, out _))
                throw new InvalidOperationException(
                    $"SaveProvider with key '{provider.SaveKey}' already registered."
                );

            var schematic = _options.Serializer.CreateSchematic(typeof(TState));
            schematic.SchemaVersion = provider.SchemaVersion;

            _providers.Add(provider.SaveKey, new ProviderEntry
            {
                Provider = provider,
                Schematic = schematic,
                StateType = typeof(TState),
                CaptureState = () => provider.CaptureState()!,
                RestoreState = state => provider.RestoreState((TState)state)
            });
            _registrationsValidated = false;
        }

        /// <summary>
        /// Registers a typed save provider without persistence. The provider will participate in snapshots
        /// but will not be written to or read from disk (memory-only storage, e.g. for caching).
        /// </summary>
        /// <remarks>
        /// Use this method only when the provider state is intentionally memory-only. The provider is included in
        /// <see cref="CaptureSnapshot"/> and <see cref="RestoreSnapshot"/>, but no provider file is written to disk.
        /// Call <see cref="ValidateRegistrations"/> after registration is complete and before disk save/load operations.
        /// </remarks>
        /// <param name="provider">The save provider to register.</param>
        public void RegisterMemoryProvider<TState>(ISaveProvider<TState> provider)
        {
            ValidateProvider(provider);

            if (_providers.TryGetValue(provider.SaveKey, out _))
                throw new InvalidOperationException(
                    $"SaveProvider with key '{provider.SaveKey}' already registered."
                );

            _providers.Add(provider.SaveKey, new ProviderEntry
            {
                Provider = provider,
                Schematic = null,
                StateType = typeof(TState),
                CaptureState = () => provider.CaptureState()!,
                RestoreState = state => provider.RestoreState((TState)state)
            });
            _registrationsValidated = false;
        }

        /// <summary>
        /// Unregisters a save provider by instance. The provider will no longer be included in save/load operations.
        /// </summary>
        /// <remarks>
        /// This overload removes the provider only when the registered provider instance matches
        /// <paramref name="provider"/>. To remove by provider key, use <see cref="UnregisterProvider(string)"/>.
        /// Call <see cref="ValidateRegistrations"/> again before the next disk save/load operation if this returns true.
        /// </remarks>
        /// <param name="provider">The save provider instance to unregister. If null, this method returns false.</param>
        /// <returns>True when the registered provider was removed; otherwise, false.</returns>
        public bool UnregisterProvider(ISaveProvider provider)
        {
            if (provider == null)
                return false;

            if (!_providers.TryGetValue(provider.SaveKey, out var providerEntry))
                return false;

            if (!ReferenceEquals(providerEntry.Provider, provider))
                return false;

            _providers.Remove(provider.SaveKey);
            _registrationsValidated = false;
            return true;
        }

        /// <summary>
        /// Unregisters a save provider by save key. The provider will no longer be included in save/load operations.
        /// </summary>
        /// <remarks>
        /// Use this overload when only the provider key is available or when removal by key is explicitly intended.
        /// Call <see cref="ValidateRegistrations"/> again before the next disk save/load operation if this returns true.
        /// </remarks>
        /// <param name="saveKey">The save key of the provider to unregister.</param>
        /// <returns>True when a registered provider was removed; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="saveKey"/> is null, empty, or whitespace.</exception>
        public bool UnregisterProvider(string saveKey)
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                throw new ArgumentException("SaveKey cannot be null, empty, or whitespace.", nameof(saveKey));

            if (!_providers.Remove(saveKey))
                return false;

            _registrationsValidated = false;
            return true;
        }

        private static void ValidateProvider(ISaveProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(provider.SaveKey))
                throw new ArgumentException("SaveProvider SaveKey cannot be null, empty, or whitespace.", nameof(provider));
        }

        /// <summary>
        /// Validates all registered persisted providers before disk save/load operations are allowed.
        /// </summary>
        /// <remarks>
        /// Registration itself is intentionally lightweight. This method performs provider state capture,
        /// serializer compatibility checks, file-name checks, and migration policy validation at the caller's
        /// chosen setup point. Call this again after registering or unregistering providers.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when any persisted provider registration is invalid.</exception>
        public void ValidateRegistrations()
        {
            ValidateFileExtension(_options.Serializer.FileExtension);

            foreach (var providerEntry in _providers.Values)
            {
                ValidateProvider(providerEntry.Provider);

                var schematic = providerEntry.Schematic;
                if (schematic == null)
                    continue;

                schematic.SchemaVersion = providerEntry.Provider.SchemaVersion;

                var context = new SaveFileContext(
                    providerEntry.Provider.SaveKey,
                    providerEntry.Provider.SchemaVersion,
                    _options.Serializer.GetType());
                ValidateFileName(_options.FileNameResolver(context));

                ValidateProviderSerialization(providerEntry, schematic);
                ValidateProviderMigration(providerEntry);
            }

            _registrationsValidated = true;
        }

        /// <summary>
        /// Lists the resolved save slot folder names currently available in the configured save root.
        /// </summary>
        /// <remarks>
        /// This method returns resolved folder names rather than <typeparamref name="TIdentity"/> values because
        /// custom save-name resolvers are not guaranteed to be reversible. Backup folders, temp folders,
        /// to-delete folders, and folders without save metadata are ignored.
        /// </remarks>
        /// <returns>A deterministic, ordinally sorted list of resolved save slot folder names.</returns>
        public IReadOnlyList<string> ListSaveSlots()
        {
            if (!Directory.Exists(_options.SaveRootPath))
                return Array.Empty<string>();

            return Directory.EnumerateDirectories(_options.SaveRootPath)
                .Where(IsSaveSlotFolder)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private void ValidateProviderSerialization(ProviderEntry providerEntry, ISaveSchematic schematic)
        {
            object state;
            try
            {
                state = providerEntry.CaptureState();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SaveProvider with key '{providerEntry.Provider.SaveKey}' failed while capturing state during registration validation.",
                    ex);
            }

            try
            {
                _options.Serializer.Serialize(state, schematic);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SaveProvider with key '{providerEntry.Provider.SaveKey}' produces incompatible state for its registered schematic.",
                    ex);
            }
        }

        private void ValidateProviderMigration(ProviderEntry providerEntry)
        {
            if (!(providerEntry.Provider is ISaveMigratable migratable))
                return;

            if (!(_options.Serializer is ISaveMigrationCapableSerializer))
            {
                throw new InvalidOperationException(
                    $"SaveProvider with key '{providerEntry.Provider.SaveKey}' implements ISaveMigratable but the serializer ({_options.Serializer.GetType().Name}) does not implement ISaveMigrationCapableSerializer. " +
                    "Migration-capable providers require migration-capable serializers.");
            }

            var migrationSource = migratable.CreateMigrationSource();
            _migrationEngine.ValidateMigrationPolicy(providerEntry.Provider.SaveKey, migrationSource, providerEntry.Provider.SchemaVersion);
        }

        private void EnsureRegistrationsValidated()
        {
            if (!_registrationsValidated)
                throw new InvalidOperationException(
                    "Save registrations must be validated with ValidateRegistrations() before disk save/load operations.");
        }

        /// <summary>
        /// Captures the current state of all registered providers into a snapshot.
        /// Providers are captured in order of their <see cref="ISaveProvider.LoadPriority"/>.
        /// </summary>
        /// <returns>A snapshot containing the current state of all registered providers.</returns>
        public SaveSnapshot CaptureSnapshot()
        {
            var snapshot = new SaveSnapshot();

            foreach (var provider in _providers.Values
                         .OrderBy(p => p.Provider.LoadPriority))
            {
                if (provider.Provider is ISaveLifecycle lifecycle)
                    lifecycle.OnBeforeSave();

                snapshot.Add(
                    provider.Provider.SaveKey,
                    provider.Provider.SchemaVersion,
                    provider.CaptureState(),
                    provider.Provider.LoadPriority
                );
            }

            return snapshot;
        }

        /// <summary>
        /// Restores the state of all registered providers from a snapshot.
        /// Providers are restored in order of their load priority, then all providers receive
        /// an <see cref="ISaveLifecycle.OnAfterLoad"/> callback if they implement <see cref="ISaveLifecycle"/>.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore from. Providers not present in the snapshot are skipped.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the snapshot is invalid for the current registrations.</exception>
        /// <remarks>
        /// This method validates the snapshot before mutating providers. If validation passes but a provider throws
        /// from its restore method, earlier providers may already have been restored.
        /// </remarks>
        public void RestoreSnapshot(SaveSnapshot snapshot)
        {
            ValidateSnapshotForRestore(snapshot);

            foreach (var entry in snapshot.Entries
                         .OrderBy(e => e.LoadPriority))
            {
                if (!_providers.TryGetValue(entry.SaveKey, out var provider))
                    continue;

                provider.RestoreState(entry.State);
            }

            foreach (var provider in _providers.Values)
            {
                if (provider.Provider is ISaveLifecycle lifecycle)
                    lifecycle.OnAfterLoad();
            }
        }

        /// <summary>
        /// Validates that a snapshot can be restored by the currently registered providers.
        /// </summary>
        /// <remarks>
        /// Snapshot validation rejects duplicate entries, unknown provider keys, schema mismatches, and persisted
        /// provider states that are incompatible with their registered schematic. Registered providers that are
        /// absent from the snapshot are allowed and will be skipped during restore.
        /// </remarks>
        /// <param name="snapshot">The snapshot to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the snapshot cannot be restored by this manager.</exception>
        public void ValidateSnapshotForRestore(SaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var seenKeys = new HashSet<string>();
            foreach (var entry in snapshot.Entries)
            {
                if (!seenKeys.Add(entry.SaveKey))
                {
                    throw new InvalidOperationException(
                        $"Snapshot contains multiple entries for provider key '{entry.SaveKey}'.");
                }

                if (!_providers.TryGetValue(entry.SaveKey, out var providerEntry))
                {
                    throw new InvalidOperationException(
                        $"Snapshot contains entry for unregistered provider key '{entry.SaveKey}'.");
                }

                if (entry.SchemaVersion != providerEntry.Provider.SchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Snapshot entry for provider '{entry.SaveKey}' has schema version {entry.SchemaVersion}, but the registered provider expects {providerEntry.Provider.SchemaVersion}.");
                }

                ValidateSnapshotEntryState(entry, providerEntry);
            }
        }

        private void ValidateSnapshotEntryState(SaveSnapshot.Entry entry, ProviderEntry providerEntry)
        {
            if (!providerEntry.StateType.IsInstanceOfType(entry.State))
            {
                throw new InvalidOperationException(
                    $"Snapshot entry for provider '{entry.SaveKey}' contains state incompatible with the registered provider state type.");
            }

            var schematic = providerEntry.Schematic;
            if (schematic == null)
                return;

            try
            {
                _options.Serializer.Serialize(entry.State, schematic);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Snapshot entry for provider '{entry.SaveKey}' contains state incompatible with the registered schematic.",
                    ex);
            }
        }

        internal SerializedSnapshot SerializeSnapshot(SaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var serialized = new SerializedSnapshot();

            foreach (var entry in snapshot.Entries)
            {
                if (!_providers.TryGetValue(entry.SaveKey, out var providerEntry))
                    continue; // provider no longer exists, ignore

                var schematic = providerEntry.Schematic;
                if (schematic == null)
                    continue; // provider opted out of serialization

                var serializedState = _options.Serializer.Serialize(entry.State, schematic);
                serialized.Data.Add(entry.SaveKey, new SerializedSnapshot.SerializedEntry
                {
                    SchemaVersion = entry.SchemaVersion,
                    Data = serializedState
                });
            }

            return serialized;
        }

        internal SaveSnapshot DeserializeSnapshot(SerializedSnapshot serialized)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            var snapshot = new SaveSnapshot();

            foreach (var kvp in serialized.Data)
            {
                if (!_providers.TryGetValue(kvp.Key, out var providerEntry))
                    continue; // provider no longer exists

                var schematic = providerEntry.Schematic;
                if (schematic == null)
                    continue;

                var savedSchemaVersion = kvp.Value.SchemaVersion;
                var currentSchemaVersion = providerEntry.Provider.SchemaVersion;
                var serializedData = kvp.Value.Data;

                // Apply migrations if schema versions differ and provider opts into migration
                if (savedSchemaVersion != currentSchemaVersion && providerEntry.Provider is ISaveMigratable migratable)
                {
                    // Create migration source on-demand
                    var migrationSource = migratable.CreateMigrationSource();
                    
                    // Check if migration path exists and apply if possible
                    if (!_migrationEngine.TryApplyMigrations(
                        kvp.Key,
                        ref serializedData,
                        savedSchemaVersion,
                        currentSchemaVersion,
                        migrationSource))
                    {
                        throw new InvalidOperationException(
                            $"Failed to migrate save data for provider '{kvp.Key}' from schema version {savedSchemaVersion} to {currentSchemaVersion}."
                        );
                    }
                }

                var state = _options.Serializer.Deserialize(serializedData, schematic);

                snapshot.Add(
                    kvp.Key,
                    currentSchemaVersion, // Use current schema version (migrated or already current)
                    state,
                    providerEntry.Provider.LoadPriority
                );
            }

            return snapshot;
        }

        /// <summary>
        /// Saves the current state of all registered providers to disk for the specified identity.
        /// Uses atomic file operations to ensure data integrity. If backups are enabled, creates a backup
        /// of the previous save before writing the new one.
        /// </summary>
        /// <param name="identity">The identity that identifies which save slot to write to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails (invalid save name, missing files, deserialization errors, etc.).</exception>
        /// <remarks>
        /// Call <see cref="ValidateRegistrations"/> successfully after provider registration and before calling this method.
        /// </remarks>
        public void SaveToDisk(TIdentity identity)
        {
            var saveName = ResolveSaveName(identity);
            EnsureRegistrationsValidated();
            var folderPath = GetMainFolderPath(saveName);
            var tempPath = GetTempFolderPath(saveName, _options.TempFolderName);
            var metaPath = GetMetadataFilePath(tempPath);

            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);

            Directory.CreateDirectory(tempPath);

            try
            {
                var snapshot = CaptureSnapshot();
                var serialized = SerializeSnapshot(snapshot);

                foreach (var serializedEntry in serialized.Data)
                {
                    if (!_providers.TryGetValue(serializedEntry.Key, out var providerEntry))
                        continue;

                    SaveSerializedEntryToDiskTMP(
                        serializedEntry.Key,
                        serializedEntry.Value.SchemaVersion,
                        serializedEntry.Value.Data,
                        providerEntry,
                        tempPath);
                }

                SaveMetadata? metadata;

                var existingMetaPath = GetMetadataFilePath(folderPath);
                if (File.Exists(existingMetaPath))
                {
                    metadata = JsonConvert.DeserializeObject<SaveMetadata>(
                        File.ReadAllText(existingMetaPath)) ?? SaveMetadata.CreateNewMetadata();
                }
                else
                {
                    metadata = SaveMetadata.CreateNewMetadata();
                }

                File.WriteAllText(
                    metaPath,
                    JsonConvert.SerializeObject(metadata, Formatting.Indented));

                ValidateTempSaveFolderSave(tempPath, serialized.Data);
            }
            catch
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
                throw;
            }

            PerformAtomicSwap(folderPath);
        }

        /// <summary>
        /// Loads the saved state from disk for the specified identity and restores it to all registered providers.
        /// Automatically attempts to recover from incomplete saves if detected.
        /// </summary>
        /// <param name="identity">The identity that identifies which save slot to load from.</param>
        /// <returns>True if a save was found and loaded successfully, false if no save exists for this identity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when registrations have not been validated or saved data cannot be loaded.</exception>
        /// <remarks>
        /// Call <see cref="ValidateRegistrations"/> successfully after provider registration and before calling this method.
        /// Registered persisted providers require matching files by default. Configure
        /// <see cref="SaveSystemOptions{TIdentity}.MissingProviderFileBehavior"/> to intentionally skip missing provider files.
        /// </remarks>
        public bool LoadFromDisk(TIdentity identity)
        {
            var folderPath = GetSaveFolderPath(identity);
            EnsureRegistrationsValidated();

            RecoverSave(identity);

            if (!Directory.Exists(folderPath))
                return false;

            var serialized = LoadSerializedSnapshotFromFolder(folderPath);
            var snapshot = DeserializeSnapshot(serialized);
            RestoreSnapshot(snapshot);

            return true;
        }

        /// <summary>
        /// Loads a backup slot for the given identity and restores it to all registered providers.
        /// Backup slots are numbered starting from 1, where 1 is the most recent backup.
        /// </summary>
        /// <param name="identity">The identity that identifies which save's backup to load.</param>
        /// <param name="slotNumber">The backup slot number (1-based, e.g., 1 = _0001, 2 = _0002). Must be at least 1.</param>
        /// <returns>True if the backup slot was found and loaded successfully, false if the backup doesn't exist or backups are disabled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="slotNumber"/> is less than 1 or greater than <see cref="SaveSystemOptions{TIdentity}.BackupSystemMaxBackupCount"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when registrations have not been validated or saved data cannot be loaded.</exception>
        /// <remarks>
        /// Call <see cref="ValidateRegistrations"/> successfully after provider registration and before calling this method.
        /// Registered persisted providers require matching files by default. Configure
        /// <see cref="SaveSystemOptions{TIdentity}.MissingProviderFileBehavior"/> to intentionally skip missing provider files.
        /// If the backup system is disabled, this method logs a warning and returns false without throwing an exception.
        /// </remarks>
        public bool LoadBackupSlotFromDisk(TIdentity identity, int slotNumber)
        {
            var saveName = ResolveSaveName(identity);
            EnsureRegistrationsValidated();

            if (!_options.EnableBackupSystem)
            {
                _diagnostics.LogWarning(
                    "Cannot load backup slot: Backup system is disabled. Enable backups in SaveSystemOptions to use backup slots."
                );
                return false;
            }

            if (slotNumber < 1 || slotNumber > _options.BackupSystemMaxBackupCount)
                throw new ArgumentException(
                    $"Backup slot number must be between 1 and {_options.BackupSystemMaxBackupCount}, but got {slotNumber}.",
                    nameof(slotNumber)
                );

            var backupSuffix = $"_{slotNumber:D4}";
            var backupFolderPath = _backupManager.GetBackupFolderPath(saveName, backupSuffix);

            if (!Directory.Exists(backupFolderPath))
                return false;

            var serialized = LoadSerializedSnapshotFromFolder(backupFolderPath);
            var snapshot = DeserializeSnapshot(serialized);
            RestoreSnapshot(snapshot);

            return true;
        }

        /// <summary>
        /// Attempts to recover an incomplete save operation. This is automatically called by <see cref="LoadFromDisk"/>.
        /// Checks for temporary save folders and attempts to complete or restore interrupted save operations.
        /// </summary>
        /// <param name="identity">The identity of the save to recover.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when recovery fails due to data corruption or tampering.</exception>
        public void RecoverSave(TIdentity identity)
        {
            var saveName = ResolveSaveName(identity);
            var folderPath = GetMainFolderPath(saveName);
            var tempPath = GetTempFolderPath(saveName, _options.TempFolderName);
            var toDeletePath = GetToDeleteFolderPath(saveName);

            if (!Directory.Exists(folderPath) && Directory.Exists(toDeletePath) && Directory.Exists(tempPath))
            {
                Directory.Move(tempPath, folderPath);
                Directory.Delete(toDeletePath, true);
                return;
            }

            if (Directory.Exists(folderPath) && Directory.Exists(toDeletePath) && !Directory.Exists(tempPath))
            {
                Directory.Delete(toDeletePath, true);
                return;
            }

            if (Directory.Exists(tempPath))
            {
                ValidateTempSaveFolderFromDisk(tempPath);
                var mainMeta = TryReadSaveMetadata(folderPath);
                var tempMeta = TryReadSaveMetadata(tempPath);
                if (tempMeta == null || string.IsNullOrEmpty(tempMeta.SaveId))
                    throw new InvalidOperationException(
                        $"Recovery failed: temp save at '{tempPath}' has no valid SaveId in {SaveMetadataFileName}.");
                if (mainMeta != null && mainMeta.SaveId != tempMeta.SaveId)
                    throw new InvalidOperationException(
                        $"Recovery failed: SaveId mismatch between main and temp (possible tampering). Main: '{mainMeta.SaveId}', temp: '{tempMeta.SaveId}'.");
                PerformAtomicSwap(folderPath);
            }
        }

        private void SaveSerializedEntryToDiskTMP(string saveKey, int schemaVersion, string serializedData, ProviderEntry providerEntry, string tempPath)
        {
            var context = new SaveFileContext(
                saveKey,
                schemaVersion,
                _options.Serializer.GetType()
            );

            var baseName = _options.FileNameResolver(context);
            ValidateFileName(baseName);
            var fileExtension = _options.Serializer.FileExtension;
            ValidateFileExtension(fileExtension);
            var fileName = baseName + fileExtension;
            var filePath = GetProviderFilePath(tempPath, fileName);

            File.WriteAllText(filePath, serializedData);
        }

        private string? LoadSerializedEntryFromDisk(ProviderEntry providerEntry, string folderPath)
        {
            var schematic = providerEntry.Schematic;
            if (schematic == null)
                return null;

            var context = new SaveFileContext(
                providerEntry.Provider.SaveKey,
                providerEntry.Provider.SchemaVersion,
                _options.Serializer.GetType()
            );

            var baseName = _options.FileNameResolver(context);
            ValidateFileName(baseName);
            var fileExtension = _options.Serializer.FileExtension;
            ValidateFileExtension(fileExtension);
            var fileName = baseName + fileExtension;
            var filePath = GetProviderFilePath(folderPath, fileName);

            if (!File.Exists(filePath))
            {
                if (_options.MissingProviderFileBehavior == MissingProviderFileBehavior.Skip)
                    return null;

                throw new InvalidOperationException(
                    $"Missing save file for registered provider '{providerEntry.Provider.SaveKey}' at '{filePath}'. " +
                    $"Set {nameof(SaveSystemOptions<TIdentity>.MissingProviderFileBehavior)} to {nameof(MissingProviderFileBehavior.Skip)} to intentionally skip missing provider files.");
            }

            return File.ReadAllText(filePath);
        }

        private IEnumerable<KeyValuePair<string, ProviderEntry>> PersistedProviders()
        {
            return _providers.Where(kvp => kvp.Value.Schematic != null);
        }

        private string GetSaveFolderPath(TIdentity identity)
        {
            return GetMainFolderPath(ResolveSaveName(identity));
        }

        private string ResolveSaveName(TIdentity identity)
        {
            if (identity is null)
                throw new ArgumentNullException(nameof(identity));

            var saveName = _options.SaveNameResolver(identity);
            ValidateSaveName(saveName);

            return saveName;
        }

        private void PerformAtomicSwap(string folderPath)
        {
            var saveName = Path.GetFileName(folderPath);
            var tempPath = GetTempFolderPath(saveName, _options.TempFolderName);
            var toDeletePath = GetToDeleteFolderPath(saveName);

            string? excessiveBackupPath = null;
            if (_options.EnableBackupSystem)
                excessiveBackupPath = _backupManager.PrepareExistingBackupsForNewBackup(saveName);

            var oldSaveNewPath = _backupManager.GetOldSaveDestinationPath(saveName, toDeletePath);

            if (Directory.Exists(folderPath))
            {
                Directory.Move(folderPath, oldSaveNewPath); 
            }

            Directory.Move(tempPath, folderPath);

            _backupManager.CleanupAfterSwap(toDeletePath, excessiveBackupPath);
        }


        private void ValidateTempSaveFolderSave(
            string folderPath,
            Dictionary<string, SerializedSnapshot.SerializedEntry> serializedEntries)
        {
            IEnumerable<KeyValuePair<string, ProviderEntry>> serializableProviders = PersistedProviders();

            int expectedFileCount = serializableProviders.Count();
            var (allFilesExist, missingFiles) = CheckFilesExist(serializableProviders, folderPath, serializedEntries);
            if (!allFilesExist)
                throw new InvalidOperationException(
                    $"Expected {expectedFileCount} files to exist in the temp save folder, but some were missing. Missing files: {string.Join(", ", missingFiles)}"
                );

            var (allFilesDeserialize, failedFiles) = CheckIfAllFilesDeserialize(serializableProviders, folderPath, serializedEntries);
            if (!allFilesDeserialize)
                throw new InvalidOperationException(
                    $"Expected all {expectedFileCount} files to deserialize correctly, but some did not. Failed files: {string.Join(", ", failedFiles)}"
                );

            ValidateMetadataFile(folderPath);
        }

        private (bool allFilesExist, List<string> missingFiles) CheckFilesExist(
            IEnumerable<KeyValuePair<string, ProviderEntry>> serializableProviders,
            string folderPath,
            Dictionary<string, SerializedSnapshot.SerializedEntry> serializedEntries)
        {
            bool allFilesExist = true;
            List<string> missingFiles = new List<string>();
            foreach (var provider in serializableProviders)
            {
                var context = new SaveFileContext(
                    provider.Key,
                    serializedEntries[provider.Key].SchemaVersion,
                    _options.Serializer.GetType()
                );
                var filePath = GetProviderFilePath(folderPath, context);
                if (!File.Exists(filePath))
                {
                    allFilesExist = false;
                    missingFiles.Add(filePath);
                }
            }
            return (allFilesExist, missingFiles);
        }

        private (bool allFilesDeserialize, List<string> failedFiles) CheckIfAllFilesDeserialize(
            IEnumerable<KeyValuePair<string, ProviderEntry>> serializableProviders,
            string folderPath,
            Dictionary<string, SerializedSnapshot.SerializedEntry> serializedEntries)
        {
            bool allFilesDeserialize = true;
            List<string> failedFiles = new List<string>();
            foreach (var provider in serializableProviders)
            {
                var context = new SaveFileContext(
                    provider.Key,
                    serializedEntries[provider.Key].SchemaVersion,
                    _options.Serializer.GetType()
                );
                var filePath = GetProviderFilePath(folderPath, context);
                var serializedData = File.ReadAllText(filePath);
                var schematic = provider.Value.Schematic;
                if (schematic == null)
                    continue;

                var state = _options.Serializer.Deserialize(serializedData, schematic);
                if (state == null)
                {
                    allFilesDeserialize = false;
                    failedFiles.Add(filePath);
                    break;
                }
            }
            return (allFilesDeserialize, failedFiles);
        }

        private void ValidateMetadataFile(string pathContainingMetadata)
        {
            var metaPath = GetMetadataFilePath(pathContainingMetadata);

            if (!File.Exists(metaPath))
                throw new InvalidOperationException(
                    $"No {SaveMetadataFileName} found in the temp save folder at '{metaPath}'."
                );

            SaveMetadata? metadata;
            try
            {
                var jsonContent = File.ReadAllText(metaPath);
                metadata = JsonConvert.DeserializeObject<SaveMetadata>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize {SaveMetadataFileName} from temp save folder at '{metaPath}'",
                    ex
                );
            }

            if (metadata == null)
                throw new InvalidOperationException(
                    $"Deserialized {SaveMetadataFileName} from temp save folder at '{metaPath}' resulted in null."
                );

            if (string.IsNullOrEmpty(metadata.SaveId))
                throw new InvalidOperationException(
                    $"SaveId in {SaveMetadataFileName} from temp save folder at '{metaPath}' is null or empty."
                );
        }

        private SaveMetadata? TryReadSaveMetadata(string folderPath)
        {
            var metaPath = GetMetadataFilePath(folderPath);
            if (!File.Exists(metaPath))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<SaveMetadata>(File.ReadAllText(metaPath));
            }
            catch
            {
                return null;
            }
        }

        private bool IsSaveSlotFolder(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(folderName))
                return false;

            if (string.Equals(folderName, BackupFolderName, StringComparison.Ordinal))
                return false;

            if (folderName.EndsWith(_options.TempFolderName, StringComparison.Ordinal))
                return false;

            if (folderName.EndsWith(ToDeleteFolderName, StringComparison.Ordinal))
                return false;

            return File.Exists(GetMetadataFilePath(folderPath));
        }

        private void ValidateTempSaveFolderFromDisk(string saveFolderPath)
        {
            var serializedEntries = PersistedProviders()
                .ToDictionary(kvp => kvp.Key, kvp => new SerializedSnapshot.SerializedEntry
                {
                    SchemaVersion = kvp.Value.Provider.SchemaVersion,
                    Data = string.Empty
                });
            ValidateTempSaveFolderSave(saveFolderPath, serializedEntries);
        }

        private void ValidateSaveName(string saveName)
        {
            if (string.IsNullOrWhiteSpace(saveName))
                throw new InvalidOperationException(
                    "SaveNameResolver returned null, empty, or whitespace."
                );

            if (saveName == "." || saveName == "..")
                throw new InvalidOperationException(
                    "SaveNameResolver returned an invalid directory name."
                );

            var invalidChars = Path.GetInvalidFileNameChars();
            if (saveName.IndexOfAny(invalidChars) >= 0)
                throw new InvalidOperationException(
                    $"SaveNameResolver returned a name containing invalid path characters: '{saveName}'"
                );
        }

        private void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException(
                    "FileNameResolver returned null, empty, or whitespace."
                );

            if (fileName == "." || fileName == "..")
                throw new InvalidOperationException(
                    "FileNameResolver returned an invalid file name."
                );

            var invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
                throw new InvalidOperationException(
                        $"FileNameResolver returned a name containing invalid characters: '{fileName}'"
                    );
        }

        private void ValidateFileExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new InvalidOperationException(
                    "Serializer returned an invalid file extension."
                );

            if (!extension.StartsWith("."))
                throw new InvalidOperationException(
                    $"File extension '{extension}' must start with '.'."
                );

            var invalidChars = Path.GetInvalidFileNameChars();
            if (extension.IndexOfAny(invalidChars) >= 0)
                throw new InvalidOperationException(
                    $"File extension '{extension}' contains invalid characters."
                );
        }

        /// <summary>
        /// Loads a serialized snapshot from the specified folder by reading all provider save files.
        /// </summary>
        private SerializedSnapshot LoadSerializedSnapshotFromFolder(string folderPath)
        {
            var serialized = new SerializedSnapshot();

            foreach (var kvp in PersistedProviders())
            {
                var serializedEntry = LoadSerializedEntryFromDisk(kvp.Value, folderPath);
                if (serializedEntry == null)
                    continue;

                // Extract the saved schema version from the serialized data using the serializer
                int savedSchemaVersion = ExtractSchemaVersionFromSerializedData(
                    serializedEntry,
                    kvp.Value.Provider.SchemaVersion);

                serialized.Data[kvp.Key] = new SerializedSnapshot.SerializedEntry
                {
                    SchemaVersion = savedSchemaVersion,
                    Data = serializedEntry
                };
            }

            return serialized;
        }

        /// <summary>
        /// Extracts the schema version from serialized data using the serializer.
        /// Throws an exception if the schema version cannot be extracted, as it is foundational to the save system.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the schema version cannot be extracted from the serialized data.</exception>
        private int ExtractSchemaVersionFromSerializedData(
            string serializedData,
            int currentSchemaVersion)
        {
            // Use the serializer to extract the schema version
            try
            {
                return _options.Serializer.ExtractSchemaVersion(serializedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to extract schema version from serialized save data. Deserialization will not proceed.",
                    ex
                );
            }
        }
    }
}
