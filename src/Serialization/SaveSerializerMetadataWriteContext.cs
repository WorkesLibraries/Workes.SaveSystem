using System;
using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Context passed to serializers when save-system metadata is being written.
    /// </summary>
    public sealed class SaveSerializerMetadataWriteContext
    {
        internal SaveSerializerMetadataWriteContext(
            SaveSerializerMetadata metadata,
            IReadOnlyList<SaveSerializerProviderInfo> providers)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        /// <summary>
        /// Gets the mutable serializer-owned metadata for this save.
        /// </summary>
        public SaveSerializerMetadata Metadata { get; }

        /// <summary>
        /// Gets the persisted providers included in this save.
        /// </summary>
        public IReadOnlyList<SaveSerializerProviderInfo> Providers { get; }
    }
}
