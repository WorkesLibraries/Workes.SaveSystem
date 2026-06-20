using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides the migration steps available for a migratable save provider.
    /// </summary>
    public interface ISaveMigrationSource
    {
        /// <summary>
        /// Gets the non-null set of migration steps supported by the provider.
        /// </summary>
        /// <remarks>
        /// The list itself and each migration step entry must be non-null.
        /// </remarks>
        IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }
}
