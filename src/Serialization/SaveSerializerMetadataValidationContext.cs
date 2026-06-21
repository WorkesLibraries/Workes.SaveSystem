using System;
using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Context passed to serializers when serializer-owned save metadata is validated.
    /// </summary>
    public sealed class SaveSerializerMetadataValidationContext
    {
        internal SaveSerializerMetadataValidationContext(
            SaveSerializerMetadata metadata,
            IReadOnlyList<SaveSerializerProviderInfo> providers)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        /// <summary>
        /// Gets the serializer-owned metadata stored with this save.
        /// </summary>
        public SaveSerializerMetadata Metadata { get; }

        /// <summary>
        /// Gets the currently registered persisted providers.
        /// </summary>
        public IReadOnlyList<SaveSerializerProviderInfo> Providers { get; }
    }
}
