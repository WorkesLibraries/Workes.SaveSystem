using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Read-only metadata for a save folder or backup folder.
    /// </summary>
    /// <remarks>
    /// This type exposes core-owned metadata maintained by the save system. Application-owned display metadata
    /// is intentionally not part of this contract yet.
    /// </remarks>
    public sealed class SaveMetadataInfo
    {
        internal SaveMetadataInfo(string saveId, DateTimeOffset createdAtUtc, DateTimeOffset lastWrittenAtUtc)
        {
            SaveId = saveId;
            CreatedAtUtc = createdAtUtc;
            LastWrittenAtUtc = lastWrittenAtUtc;
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
    }
}
