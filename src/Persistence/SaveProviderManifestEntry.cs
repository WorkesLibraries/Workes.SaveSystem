using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Serializer-facing metadata describing one persisted provider file written with a save.
    /// </summary>
    [Serializable]
    public sealed class SaveProviderManifestEntry
    {
        /// <summary>
        /// Gets or sets the provider save key.
        /// </summary>
        public string SaveKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider schema version stored in the save.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets the provider file name written for this entry.
        /// </summary>
        public string FileName { get; set; } = string.Empty;
    }
}
