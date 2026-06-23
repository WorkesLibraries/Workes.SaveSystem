using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Handles migration of save data between schema versions.
    /// Applies migration steps sequentially to upgrade data from older versions to current versions.
    /// </summary>
    internal sealed class MigrationEngine
    {
        private readonly ISaveSerializer _serializer;
        private readonly SaveSystemDiagnostics _diagnostics;

        public MigrationEngine(ISaveSerializer serializer, SaveSystemDiagnostics diagnostics)
        {
            _serializer = serializer;
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Validates the migration policy for a migratable provider.
        /// Ensures there are no duplicate migration steps for the same version.
        /// </summary>
        public void ValidateMigrationPolicy(string saveKey, ISaveMigrationSource migrationSource, int currentSchemaVersion)
        {
            if (migrationSource == null)
                throw new InvalidOperationException(
                    $"SaveProvider with key '{saveKey}' implements ISaveMigratable but MigrationSource is null."
                );

            var migrations = migrationSource.Migrations;
            if (migrations == null)
                throw new InvalidOperationException(
                    $"SaveProvider with key '{saveKey}' has a null Migrations list."
                );

            if (migrations.Any(m => m == null))
                throw new InvalidOperationException(
                    $"SaveProvider with key '{saveKey}' has a Migrations list that contains null entries."
                );

            var versionGroups = migrations.GroupBy(m => m.FromVersion).ToList();
            var duplicates = versionGroups.Where(g => g.Count() > 1).ToList();

            if (duplicates.Any())
            {
                var duplicateVersions = string.Join(", ", duplicates.Select(g => $"v{g.Key}"));
                throw new InvalidOperationException(
                    $"SaveProvider with key '{saveKey}' has multiple migration steps for the same version(s): {duplicateVersions}. " +
                    "Each version can only have one migration step (x -> x+1)."
                );
            }
        }

        /// <summary>
        /// Attempts to apply migration steps sequentially to migrate data from savedSchemaVersion to currentSchemaVersion.
        /// Each migration step transforms data from version x to x+1.
        /// Returns true if migration was successful, false if no migration path exists.
        /// </summary>
        public bool TryApplyMigrations(
            string saveKey,
            ref byte[] serializedData,
            int savedSchemaVersion,
            int currentSchemaVersion,
            ISaveMigrationSource migrationSource,
            SaveSerializerContext savedContext,
            SaveSerializerContext currentContext)
        {
            if (savedSchemaVersion == currentSchemaVersion)
                return true; // No migration needed

            if (savedSchemaVersion > currentSchemaVersion)
            {
                // Downgrades are not supported - let deserialization fail naturally
                _diagnostics.LogWarning(
                    $"Cannot migrate save data for provider '{saveKey}': saved version (v{savedSchemaVersion}) is newer than current version (v{currentSchemaVersion}). " +
                    "Downgrades are not supported. Deserialization will fail."
                );
                return false;
            }

            var migrations = migrationSource.Migrations;
            if (migrations == null || migrations.Count == 0)
            {
                // No migration steps available - let deserialization fail naturally
                _diagnostics.LogWarning(
                    $"Cannot migrate save data for provider '{saveKey}': no migration steps available to migrate from v{savedSchemaVersion} to v{currentSchemaVersion}. " +
                    "Deserialization will fail."
                );
                return false;
            }

            // Build a dictionary for quick lookup of migration steps by version
            var migrationMap = migrations.ToDictionary(m => m.FromVersion, m => m);

            // Verify we have all necessary migration steps
            for (int version = savedSchemaVersion; version < currentSchemaVersion; version++)
            {
                if (!migrationMap.ContainsKey(version))
                {
                    // Missing migration step - no clean path exists, let deserialization fail naturally
                    _diagnostics.LogWarning(
                        $"Cannot migrate save data for provider '{saveKey}': missing migration step from v{version} to v{version + 1}. " +
                        $"Cannot migrate from v{savedSchemaVersion} to v{currentSchemaVersion}. Deserialization will fail."
                    );
                    return false;
                }
            }

            // Get the migration-capable serializer
            var migrationSerializer = _serializer.Migration;
            if (migrationSerializer == null)
            {
                // This should have been caught during registration, but handle gracefully
                _diagnostics.LogWarning(
                    $"Cannot migrate save data for provider '{saveKey}': serializer ({_serializer.GetType().Name}) does not provide migration support. " +
                    "This should have been caught during provider registration. Deserialization will fail."
                );
                return false;
            }

            // Convert serialized data to the provider data root node. Serializer-owned envelopes
            // remain hidden inside the migration adapter.
            ISaveDataNode dataNode;
            try
            {
                dataNode = migrationSerializer is IContextualSaveMigrationCapableSerializer contextual
                    ? contextual.DeserializeToNode(serializedData, savedContext)
                    : migrationSerializer.DeserializeToNode(serializedData);
            }
            catch (Exception ex)
            {
                // Failed to parse data - let deserialization fail naturally
                _diagnostics.LogWarning(
                    $"Failed to parse data for migration of provider '{saveKey}': {ex.Message}. " +
                    "Deserialization will proceed without migration."
                );
                return false;
            }

            // Apply migrations sequentially to the provider data root.
            var factory = migrationSerializer.NodeFactory;
            for (int version = savedSchemaVersion; version < currentSchemaVersion; version++)
            {
                var migrationStep = migrationMap[version];
                try
                {
                    migrationStep.Migrate(dataNode, factory);
                }
                catch (Exception ex)
                {
                    // Migration step failed - let deserialization fail naturally
                    _diagnostics.LogWarning(
                        $"Migration step failed for provider '{saveKey}' when migrating from v{version} to v{version + 1}: {ex.Message}. " +
                        "Deserialization will proceed without migration."
                    );
                    return false;
                }
            }

            // Convert back to serialized bytes. The serializer writes the current schema version
            // using the current context when it owns an envelope/header.
            try
            {
                serializedData = migrationSerializer is IContextualSaveMigrationCapableSerializer contextual
                    ? contextual.SerializeFromNode(dataNode, currentContext)
                    : migrationSerializer.SerializeFromNode(dataNode);
            }
            catch (Exception ex)
            {
                // Failed to serialize migrated data - let deserialization fail naturally
                _diagnostics.LogWarning(
                    $"Failed to serialize migrated data for provider '{saveKey}': {ex.Message}. " +
                    "Deserialization will proceed without migration."
                );
                return false;
            }

            return true;
        }
    }
}
