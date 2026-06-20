namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents a component that can save and restore its state.
    /// Implement this interface on classes that need to participate in the save system.
    /// </summary>
    /// <remarks>
    /// <see cref="SaveKey"/> and <see cref="SchemaVersion"/> are persistence compatibility values. Keep them stable
    /// for existing saves, and add migrations when the state shape changes.
    /// </remarks>
    public interface ISaveProvider
    {
        /// <summary>
        /// Gets a unique key that identifies this provider. Must be unique across all registered providers.
        /// </summary>
        string SaveKey { get; }

        /// <summary>
        /// Gets the schema version of this provider's state. Increment this when the state structure changes.
        /// </summary>
        int SchemaVersion { get; }

        /// <summary>
        /// Gets the load priority. Providers with lower priority values are saved/loaded first.
        /// </summary>
        int LoadPriority { get; }

        /// <summary>
        /// Captures the current state of this provider as an object that can be serialized.
        /// </summary>
        /// <returns>The current state of this provider.</returns>
        object CaptureState();

        /// <summary>
        /// Restores the state of this provider from a previously captured state object.
        /// </summary>
        /// <param name="state">The state object to restore from, as returned by <see cref="CaptureState"/>.</param>
        void RestoreState(object state);
    }
}
