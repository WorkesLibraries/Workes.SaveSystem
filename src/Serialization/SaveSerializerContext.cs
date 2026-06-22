using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides provider-specific context for serializers whose payload format depends on save metadata.
    /// </summary>
    public sealed class SaveSerializerContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SaveSerializerContext"/> class.
        /// </summary>
        /// <param name="saveKey">The provider save key.</param>
        /// <param name="schemaVersion">The schema version for the provider payload being processed.</param>
        /// <param name="stateType">The provider state type.</param>
        /// <param name="schematic">The schematic created for the provider state type.</param>
        /// <param name="serializerMetadata">The serializer-owned metadata for the containing save.</param>
        public SaveSerializerContext(
            string saveKey,
            int schemaVersion,
            Type stateType,
            ISaveSchematic schematic,
            SaveSerializerMetadata serializerMetadata)
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                throw new ArgumentException("Save key cannot be null, empty, or whitespace.", nameof(saveKey));

            SaveKey = saveKey;
            SchemaVersion = schemaVersion;
            StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
            Schematic = schematic ?? throw new ArgumentNullException(nameof(schematic));
            SerializerMetadata = serializerMetadata ?? throw new ArgumentNullException(nameof(serializerMetadata));
        }

        /// <summary>
        /// Gets the provider save key.
        /// </summary>
        public string SaveKey { get; }

        /// <summary>
        /// Gets the schema version for the provider payload being processed.
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

        /// <summary>
        /// Gets the serializer-owned metadata for the containing save.
        /// </summary>
        public SaveSerializerMetadata SerializerMetadata { get; }
    }
}
