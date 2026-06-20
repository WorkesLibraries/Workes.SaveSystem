using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A simple implementation of <see cref="ISaveIdentity"/> that uses a string to identify saves.
    /// Useful for save systems where slots are identified by stable names such as "Save1" or "AutoSave".
    /// </summary>
    public sealed class StringSaveIdentity : ISaveIdentity
    {
        /// <summary>
        /// Gets the validated folder name that identifies this save slot.
        /// </summary>
        public string SaveName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringSaveIdentity"/> class.
        /// </summary>
        /// <param name="saveName">The name that identifies this save. Cannot be null, empty, or whitespace.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="saveName"/> is null, empty, or whitespace.</exception>
        public StringSaveIdentity(string saveName)
        {
            if (string.IsNullOrWhiteSpace(saveName))
                throw new ArgumentException("Save name cannot be null, empty, or whitespace.", nameof(saveName));

            SaveName = saveName;
        }
    }
}
