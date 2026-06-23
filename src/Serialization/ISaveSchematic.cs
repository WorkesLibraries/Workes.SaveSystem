using System.Diagnostics.CodeAnalysis;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Defines how a provider's state is serialized and deserialized.
    /// </summary>
    /// <remarks>
    /// Serializer implementations create schematics for registered provider state types. Custom serializers
    /// can implement this interface directly, but <see cref="SaveSchematic{T}"/> is the usual base for
    /// type-safe schematics.
    /// </remarks>
    public interface ISaveSchematic
    {
        /// <summary>
        /// Gets or sets the schema version. The manager sets this from the provider's <see cref="ISaveProvider.SchemaVersion"/> at registration.
        /// </summary>
        int SchemaVersion { get; set; }

        /// <summary>
        /// Serializes a provider state object to bytes.
        /// </summary>
        /// <param name="state">The state object to serialize.</param>
        /// <returns>The serialized payload bytes.</returns>
        byte[] SerializeUntyped(object state);

        /// <summary>
        /// Deserializes bytes back into a provider state object.
        /// </summary>
        /// <param name="serialized">The serialized bytes to deserialize.</param>
        /// <returns>The deserialized state object.</returns>
        [return: MaybeNull]
        object DeserializeUntyped(byte[] serialized);
    }
}
