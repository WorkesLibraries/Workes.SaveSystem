namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional extension interface for serializers that need serializer-owned metadata stored with each save.
    /// </summary>
    /// <remarks>
    /// Implement this only when the serializer format needs metadata beyond the core save-system fields,
    /// such as provider field maps or codec settings.
    /// </remarks>
    public interface ISaveSerializerMetadataHandler
    {
        /// <summary>
        /// Writes serializer-owned metadata before the save metadata file is persisted.
        /// </summary>
        /// <param name="context">The serializer metadata write context.</param>
        void WriteMetadata(SaveSerializerMetadataWriteContext context);

        /// <summary>
        /// Validates serializer-owned metadata read from an existing save folder.
        /// </summary>
        /// <param name="context">The serializer metadata validation context.</param>
        void ValidateMetadata(SaveSerializerMetadataValidationContext context);
    }
}
