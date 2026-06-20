namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional interface for save providers that need to perform actions before saving or after loading.
    /// Implement this interface on providers that need lifecycle callbacks.
    /// </summary>
    /// <remarks>
    /// <see cref="OnBeforeSave"/> runs before provider state is captured. <see cref="OnAfterLoad"/> runs after all
    /// providers in a restored snapshot have received their state.
    /// </remarks>
    public interface ISaveLifecycle
    {
        /// <summary>
        /// Called before the provider's state is captured for saving.
        /// Use this to prepare the provider for serialization.
        /// </summary>
        void OnBeforeSave();

        /// <summary>
        /// Called after the provider's state has been restored from a load operation.
        /// Use this to perform post-load initialization or validation.
        /// </summary>
        void OnAfterLoad();
    }
}
