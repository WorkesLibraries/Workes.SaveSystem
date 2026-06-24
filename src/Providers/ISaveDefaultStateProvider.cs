using System.Diagnostics.CodeAnalysis;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional provider hook used when loading old saves that predate a registered persisted provider.
    /// </summary>
    /// <typeparam name="TState">The state type restored by the provider.</typeparam>
    public interface ISaveDefaultStateProvider<TState>
    {
        /// <summary>
        /// Creates deterministic state for old saves whose provider manifest does not include this provider.
        /// </summary>
        /// <returns>The state to restore for this provider.</returns>
        [return: MaybeNull]
        TState CreateDefaultStateForMissingSave();
    }
}
