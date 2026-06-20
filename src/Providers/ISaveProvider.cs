namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents the identity and ordering metadata for a component that participates in the save system.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="ISaveProvider{TState}"/> for providers that capture and restore state. <see cref="SaveKey"/>
    /// and <see cref="SchemaVersion"/> are persistence compatibility values. Keep them stable for existing saves,
    /// and add migrations when the state shape changes. A provider's <see cref="SaveKey"/> must not change after
    /// registration, and its <see cref="SchemaVersion"/> must not change after registration validation.
    /// </remarks>
    public interface ISaveProvider
    {
        /// <summary>
        /// Gets a unique key that identifies this provider. Must be unique across all registered providers.
        /// </summary>
        /// <remarks>
        /// This value is persistent identity and must remain stable after the provider is registered.
        /// </remarks>
        string SaveKey { get; }

        /// <summary>
        /// Gets the schema version of this provider's state. Increment this when the state structure changes.
        /// </summary>
        /// <remarks>
        /// This value is part of the persisted save contract and must remain stable after registration validation.
        /// </remarks>
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
        /// <returns>The current non-null state of this provider.</returns>
        /// <remarks>
        /// Null provider state is not supported. Return an explicit empty state object when a provider has no data.
        /// </remarks>
        TState CaptureState();

        /// <summary>
        /// Restores the state of this provider from a previously captured state object.
        /// </summary>
        /// <param name="state">The state object to restore from, as returned by <see cref="CaptureState"/>.</param>
        void RestoreState(TState state);
    }
}
