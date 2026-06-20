using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides the migration steps available for a migratable save provider.
    /// </summary>
    public interface ISaveMigrationSource
    {
        /// <summary>
        /// Gets the set of migration steps supported by the provider.
        /// </summary>
        IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }
}
