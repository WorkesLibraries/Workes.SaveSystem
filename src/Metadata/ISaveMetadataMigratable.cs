namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides migration steps for application-owned save metadata.
    /// </summary>
    public interface ISaveMetadataMigratable
    {
        /// <summary>
        /// Creates the migration source used to migrate application metadata between schema versions.
        /// </summary>
        /// <returns>The application metadata migration source.</returns>
        ISaveMigrationSource CreateMetadataMigrationSource();
    }
}
