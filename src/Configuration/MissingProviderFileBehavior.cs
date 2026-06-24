namespace Workes.SaveSystem
{
    /// <summary>
    /// Defines how disk loads behave when a registered persisted provider has no save file in the save folder.
    /// </summary>
    public enum MissingProviderFileBehavior
    {
        /// <summary>
        /// Throw an exception when a registered persisted provider file is missing.
        /// </summary>
        Throw = 0,

        /// <summary>
        /// Skip registered persisted providers whose files are missing in legacy saves without a provider manifest
        /// and leave their current state unchanged. Manifest-backed saves still fail when a manifest-present provider
        /// file is missing.
        /// </summary>
        Skip = 1
    }
}
