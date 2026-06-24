using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Read-only metadata for a save folder or backup folder.
    /// </summary>
    /// <remarks>
    /// This type exposes core-owned metadata maintained by the save system. It can report whether
    /// application-owned metadata exists, but typed application metadata is read through dedicated manager APIs.
    /// </remarks>
    public sealed class SaveMetadataInfo
    {
        internal SaveMetadataInfo(
            string saveId,
            DateTimeOffset createdAtUtc,
            DateTimeOffset lastWrittenAtUtc,
            bool hasApplicationMetadata,
            int? applicationMetadataSchemaVersion)
        {
            SaveId = saveId;
            CreatedAtUtc = createdAtUtc;
            LastWrittenAtUtc = lastWrittenAtUtc;
            HasApplicationMetadata = hasApplicationMetadata;
            ApplicationMetadataSchemaVersion = applicationMetadataSchemaVersion;
        }

        /// <summary>
        /// Gets the stable save identifier used by the save system to validate recovery operations.
        /// </summary>
        public string SaveId { get; }

        /// <summary>
        /// Gets the UTC timestamp when this save metadata was first created.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// Gets the UTC timestamp when this save metadata was last written.
        /// </summary>
        public DateTimeOffset LastWrittenAtUtc { get; }

        /// <summary>
        /// Gets whether this save contains application-owned metadata.
        /// </summary>
        public bool HasApplicationMetadata { get; }

        /// <summary>
        /// Gets the schema version of the stored application metadata, or null when none exists.
        /// </summary>
        public int? ApplicationMetadataSchemaVersion { get; }
    }
}
