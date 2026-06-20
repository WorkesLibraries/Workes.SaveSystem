using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents a single migration step that transforms data from one schema version to the next.
    /// The migration action receives both the data node and a factory for creating new nodes.
    /// </summary>
    public sealed class SaveMigrationStep
    {
        /// <summary>
        /// Gets the schema version this step migrates from.
        /// </summary>
        public int FromVersion { get; }

        /// <summary>
        /// Gets the migration action that mutates a serialized data node from <see cref="FromVersion"/> to the next version.
        /// </summary>
        public Action<ISaveDataNode, ISaveDataNodeFactory> Migrate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveMigrationStep"/> class.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from (migrates to fromVersion + 1).</param>
        /// <param name="migrate">The migration action that transforms the data. Receives the data node and a factory for creating new nodes.</param>
        public SaveMigrationStep(int fromVersion, Action<ISaveDataNode, ISaveDataNodeFactory> migrate)
        {
            if (fromVersion < 1)
                throw new ArgumentException("From version must be greater than 0", nameof(fromVersion));

            if (migrate == null)
                throw new ArgumentNullException(nameof(migrate));

            FromVersion = fromVersion;
            Migrate = migrate;
        }
    }
}
