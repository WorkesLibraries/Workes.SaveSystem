namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents the identity and ordering metadata for a component that participates in the save system.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="ISaveProvider{TState}"/> for providers that capture and restore state. <see cref="SaveKey"/>
    /// and <see cref="SchemaVersion"/> are persistence compatibility values. Keep them stable for existing saves,
    /// and add migrations when the state shape changes.
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
    }

    /// <summary>
    /// Represents a component that can save and restore a specific state type.
    /// </summary>
    /// <typeparam name="TState">The state type captured and restored by this provider.</typeparam>
    public interface ISaveProvider<TState> : ISaveProvider
    {
        /// <summary>
        /// Captures the current state of this provider.
        /// </summary>
        /// <returns>The current state of this provider.</returns>
        TState CaptureState();

        /// <summary>
        /// Restores the state of this provider from a previously captured state object.
        /// </summary>
        /// <param name="state">The state object to restore from, as returned by <see cref="CaptureState"/>.</param>
        void RestoreState(TState state);
    }
}
