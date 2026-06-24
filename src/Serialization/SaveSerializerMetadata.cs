using System;
using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Serializer-owned metadata stored inside save-system metadata.
    /// </summary>
    /// <remarks>
    /// This metadata is intended for serializer format details such as field maps or codec settings.
    /// Application-owned display metadata should be stored through an application metadata provider.
    /// </remarks>
    public sealed class SaveSerializerMetadata
    {
        /// <summary>
        /// Gets serializer-owned metadata that applies to the whole save.
        /// </summary>
        public Dictionary<string, string> Global { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets serializer-owned metadata grouped by provider save key.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Providers { get; set; } =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        /// <summary>
        /// Gets existing metadata for a provider or creates an empty metadata dictionary for that provider.
        /// </summary>
        /// <param name="saveKey">The provider save key.</param>
        /// <returns>The serializer metadata dictionary for the provider.</returns>
        public Dictionary<string, string> GetOrCreateProvider(string saveKey)
        {
            if (string.IsNullOrWhiteSpace(saveKey))
                throw new ArgumentException("Save key cannot be null, empty, or whitespace.", nameof(saveKey));

            if (!Providers.TryGetValue(saveKey, out var metadata))
            {
                metadata = new Dictionary<string, string>(StringComparer.Ordinal);
                Providers.Add(saveKey, metadata);
            }

            return metadata;
        }

        internal void Normalize()
        {
            Global ??= new Dictionary<string, string>(StringComparer.Ordinal);
            Providers ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (var key in new List<string>(Providers.Keys))
            {
                if (Providers[key] == null)
                    Providers[key] = new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
    }
}
