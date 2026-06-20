using System.Collections.Generic;
using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents a snapshot of the current state of all registered save providers.
    /// </summary>
    /// <remarks>
    /// Snapshots are in-memory captures. They preserve provider keys, schema versions, load priorities,
    /// and state objects, but they do not own disk persistence by themselves.
    /// </remarks>
    public sealed class SaveSnapshot
    {
        /// <summary>
        /// Represents one provider state captured in a save snapshot.
        /// </summary>
        public sealed class Entry
        {
            internal Entry(string saveKey, int schemaVersion, object state, int loadPriority)
            {
                SaveKey = saveKey;
                SchemaVersion = schemaVersion;
                State = state;
                LoadPriority = loadPriority;
            }

            /// <summary>
            /// Gets the save key of the provider this entry belongs to.
            /// </summary>
            public string SaveKey { get; }

            /// <summary>
            /// Gets the schema version of the provider state.
            /// </summary>
            public int SchemaVersion { get; }

            /// <summary>
            /// Gets the load priority used when restoring this entry.
            /// </summary>
            public int LoadPriority { get; }

            /// <summary>
            /// Gets the captured provider state object.
            /// </summary>
            public object State { get; }
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Gets a read-only list of all entries in this snapshot.
        /// </summary>
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// Adds a provider state entry to this snapshot.
        /// </summary>
        /// <remarks>
        /// This method validates the entry before mutating the snapshot.
        /// </remarks>
        /// <param name="key">The stable save key of the provider.</param>
        /// <param name="schemaVersion">The schema version of the provider state.</param>
        /// <param name="state">The provider state object to store.</param>
        /// <param name="loadPriority">The load priority of the provider. Defaults to 0.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="schemaVersion"/> is less than 1.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public void Add(string key, int schemaVersion, object state, int loadPriority = 0)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Save key cannot be null, empty, or whitespace.", nameof(key));

            if (schemaVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than 0.");

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _entries.Add(new Entry(key, schemaVersion, state, loadPriority));
        }
    }
}
