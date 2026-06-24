using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Serializer-facing application metadata payload stored inside save-system metadata.
    /// </summary>
    /// <remarks>
    /// The <see cref="Data"/> value is produced by the active save serializer using the registered
    /// application metadata provider's typed schematic. It is serializer-owned inline data: readable JSON
    /// for JSON serializers, and format-native data for compact/binary serializers.
    /// </remarks>
    [Serializable]
    public sealed class SaveApplicationMetadata
    {
        /// <summary>
        /// Gets or sets the schema version of the serialized application metadata payload.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets serializer-owned inline application metadata data.
        /// </summary>
        public object? Data { get; set; }
    }
}
