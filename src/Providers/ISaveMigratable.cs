namespace Workes.SaveSystem
{
    /// <summary>
    /// Interface for save providers that support migration between schema versions.
    /// The migration source is created on-demand by the provider when needed.
    /// </summary>
    public interface ISaveMigratable
    {
        /// <summary>
        /// Creates a new instance of the migration source for this provider.
        /// Called by the save system when migration validation or execution is needed.
        /// </summary>
        /// <returns>A new instance of <see cref="ISaveMigrationSource"/> containing migration steps.</returns>
        ISaveMigrationSource CreateMigrationSource();
    }
}
