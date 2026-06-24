using System.Diagnostics.CodeAnalysis;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides application-owned metadata stored with a save folder.
    /// </summary>
    /// <remarks>
    /// Application metadata is intended for menu or display data such as character name, playtime,
    /// difficulty, or screenshot references. It is separate from core <see cref="SaveMetadata"/> and
    /// serializer-owned <see cref="SaveSerializerMetadata"/>.
    /// </remarks>
    /// <typeparam name="TMetadata">The metadata state type captured and restored by this provider.</typeparam>
    public interface ISaveMetadataProvider<TMetadata>
    {
        /// <summary>
        /// Gets the schema version of the application metadata state.
        /// </summary>
        int MetadataSchemaVersion { get; }

        /// <summary>
        /// Captures the current application metadata for the save being written.
        /// </summary>
        [return: MaybeNull]
        TMetadata CaptureMetadata();

        /// <summary>
        /// Restores application metadata read from a save.
        /// </summary>
        /// <param name="metadata">The metadata state read from disk.</param>
        void RestoreMetadata(TMetadata metadata);
    }
}
