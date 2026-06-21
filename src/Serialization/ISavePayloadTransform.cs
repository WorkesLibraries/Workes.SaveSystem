namespace Workes.SaveSystem
{
    /// <summary>
    /// Defines a reversible byte transform that can wrap serializer payloads.
    /// </summary>
    /// <remarks>
    /// Payload transforms are intended for format-level concerns such as compression, obfuscation, or encryption.
    /// </remarks>
    public interface ISavePayloadTransform
    {
        /// <summary>
        /// Gets the file extension suffix appended after the inner serializer extension, including the leading dot.
        /// </summary>
        string FileExtensionSuffix { get; }

        /// <summary>
        /// Encodes serialized payload bytes before they are written to disk.
        /// </summary>
        /// <param name="data">The inner serializer payload bytes.</param>
        /// <returns>The transformed payload bytes.</returns>
        byte[] Encode(byte[] data);

        /// <summary>
        /// Decodes serialized payload bytes after they are read from disk.
        /// </summary>
        /// <param name="data">The transformed payload bytes.</param>
        /// <returns>The original inner serializer payload bytes.</returns>
        byte[] Decode(byte[] data);
    }
}
