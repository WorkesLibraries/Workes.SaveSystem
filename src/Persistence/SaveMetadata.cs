using System;
using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Serializer-facing metadata payload written by the save system for each save folder.
    /// </summary>
    /// <remarks>
    /// Serializers must be able to serialize and deserialize this type because save metadata is written
    /// through the active <see cref="ISaveSerializer"/>. Application display metadata such as character
    /// name, playtime, difficulty, or screenshots is stored in <see cref="ApplicationMetadata"/> when an
    /// application metadata provider is registered. Use <see cref="SaveMetadataInfo"/> for menu/read APIs
    /// that only need core save metadata.
    /// </remarks>
    [Serializable]
    public sealed class SaveMetadata
    {
        /// <summary>
        /// Gets or sets the stable save identifier used by the save system to validate recovery operations.
        /// </summary>
        public string SaveId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when this save metadata was first created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when this save metadata was last written.
        /// </summary>
        public DateTimeOffset LastWrittenAtUtc { get; set; }

        /// <summary>
        /// Gets or sets serializer-owned metadata for advanced serializer format details.
        /// </summary>
        public SaveSerializerMetadata SerializerMetadata { get; set; } = new SaveSerializerMetadata();

        /// <summary>
        /// Gets or sets the persisted provider files written with this save.
        /// </summary>
        /// <remarks>
        /// A null value represents legacy metadata written before provider manifests existed.
        /// </remarks>
        public List<SaveProviderManifestEntry>? ProviderManifest { get; set; }

        /// <summary>
        /// Gets or sets optional application-owned metadata stored with this save.
        /// </summary>
        public SaveApplicationMetadata? ApplicationMetadata { get; set; }

        /// <summary>
        /// Creates a new metadata payload with a new save id and initialized timestamps.
        /// </summary>
        /// <param name="timestampUtc">Optional UTC timestamp to use for both created and last-written time.</param>
        /// <returns>A new save metadata payload.</returns>
        internal static SaveMetadata CreateNewMetadata(DateTimeOffset? timestampUtc = null)
        {
            var timestamp = timestampUtc ?? DateTimeOffset.UtcNow;
            return new SaveMetadata
            {
                SaveId = Guid.NewGuid().ToString(),
                CreatedAtUtc = timestamp,
                LastWrittenAtUtc = timestamp
            };
        }

        /// <summary>
        /// Ensures required metadata fields are initialized before writing.
        /// </summary>
        /// <param name="timestampUtc">The UTC timestamp to store as the last-written time.</param>
        internal void PrepareForWrite(DateTimeOffset timestampUtc)
        {
            if (string.IsNullOrEmpty(SaveId))
                SaveId = Guid.NewGuid().ToString();

            if (CreatedAtUtc == default)
                CreatedAtUtc = timestampUtc;

            LastWrittenAtUtc = timestampUtc;
            SerializerMetadata ??= new SaveSerializerMetadata();
            SerializerMetadata.Normalize();
        }
    }
}
