using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides context information about a save file being created.
    /// Used by <see cref="SaveSystemOptions{TIdentity}.FileNameResolver"/> to generate custom file names.
    /// </summary>
    /// <remarks>
    /// File-name resolvers should usually use <see cref="SaveKey"/> only. Including <see cref="SchemaVersion"/>
    /// in the file name can prevent migrations from finding older provider files.
    /// </remarks>
    public readonly struct SaveFileContext
    {
        /// <summary>
        /// Gets the save key of the provider this file belongs to.
        /// </summary>
        public string SaveKey { get; }

        /// <summary>
        /// Gets the schema version of the provider this file belongs to.
        /// </summary>
        public int SchemaVersion { get; }

        /// <summary>
        /// Gets the type of serializer being used.
        /// </summary>
        public Type SerializerType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveFileContext"/> struct.
        /// </summary>
        /// <param name="saveKey">The save key of the provider.</param>
        /// <param name="schemaVersion">The schema version of the provider.</param>
        /// <param name="serializerType">The type of serializer being used.</param>
        public SaveFileContext(
            string saveKey,
            int schemaVersion,
            Type serializerType
        )
        {
            SaveKey = saveKey;
            SchemaVersion = schemaVersion;
            SerializerType = serializerType;
        }
    }
}
