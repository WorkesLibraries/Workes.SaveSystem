namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional serializer capability for embedding typed application metadata inside save-system metadata.
    /// </summary>
    /// <remarks>
    /// Implement this when a serializer supports <see cref="ISaveMetadataProvider{TMetadata}"/>.
    /// The returned inline data is serializer-owned and stored in <see cref="SaveApplicationMetadata.Data"/>.
    /// </remarks>
    public interface ISaveApplicationMetadataSerializer
    {
        /// <summary>
        /// Converts typed application metadata into serializer-native inline data.
        /// </summary>
        object? SerializeApplicationMetadata(object? metadata, SaveSerializerContext context);

        /// <summary>
        /// Converts serializer-native inline data back into typed application metadata.
        /// </summary>
        object? DeserializeApplicationMetadata(object? data, SaveSerializerContext context);

        /// <summary>
        /// Converts serializer-native inline data into an editable migration node.
        /// </summary>
        ISaveDataNode DeserializeApplicationMetadataToNode(object? data, SaveSerializerContext context);

        /// <summary>
        /// Converts an edited migration node back into serializer-native inline data.
        /// </summary>
        object? SerializeApplicationMetadataFromNode(ISaveDataNode node, SaveSerializerContext context);
    }
}
