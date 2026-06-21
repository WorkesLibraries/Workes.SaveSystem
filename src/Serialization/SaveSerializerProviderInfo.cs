using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Describes a persisted provider when serializer metadata is written or validated.
    /// </summary>
    public sealed class SaveSerializerProviderInfo
    {
        internal SaveSerializerProviderInfo(
            string saveKey,
            int schemaVersion,
            Type stateType,
            ISaveSchematic schematic)
        {
            SaveKey = saveKey ?? throw new ArgumentNullException(nameof(saveKey));
            SchemaVersion = schemaVersion;
            StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
            Schematic = schematic ?? throw new ArgumentNullException(nameof(schematic));
        }

        /// <summary>
        /// Gets the provider save key.
        /// </summary>
        public string SaveKey { get; }

        /// <summary>
        /// Gets the provider schema version used by the current registration.
        /// </summary>
        public int SchemaVersion { get; }

        /// <summary>
        /// Gets the provider state type.
        /// </summary>
        public Type StateType { get; }

        /// <summary>
        /// Gets the schematic created for the provider state type.
        /// </summary>
        public ISaveSchematic Schematic { get; }
    }
}
