using System;
using System.IO;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Factory methods for composing serializers with reversible payload transforms.
    /// </summary>
    public static class SaveSerializerTransforms
    {
        /// <summary>
        /// Wraps a serializer with a reversible byte transform.
        /// </summary>
        /// <param name="inner">The serializer that produces and consumes untransformed payload bytes.</param>
        /// <param name="transform">The transform that encodes and decodes payload bytes.</param>
        /// <returns>A serializer that stores transformed payload bytes.</returns>
        public static ISaveSerializer Wrap(ISaveSerializer inner, ISavePayloadTransform transform)
        {
            if (inner == null)
                throw new ArgumentNullException(nameof(inner));
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            ValidateExtension(inner.FileExtension, nameof(inner));
            ValidateExtension(transform.FileExtensionSuffix, nameof(transform));

            var migrationCapableSerializer = inner as ISaveMigrationCapableSerializer;
            var metadataHandler = inner as ISaveSerializerMetadataHandler;

            if (migrationCapableSerializer != null && metadataHandler != null)
                return new MetadataMigrationCapableTransformedSaveSerializer(inner, migrationCapableSerializer, metadataHandler, transform);

            if (migrationCapableSerializer != null)
                return new MigrationCapableTransformedSaveSerializer(inner, migrationCapableSerializer, transform);

            if (metadataHandler != null)
                return new MetadataTransformedSaveSerializer(inner, metadataHandler, transform);

            return new TransformedSaveSerializer(inner, transform);
        }

        private static void ValidateExtension(string extension, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("File extension cannot be null, empty, or whitespace.", parameterName);

            if (!extension.StartsWith(".", StringComparison.Ordinal))
                throw new ArgumentException($"File extension '{extension}' must start with '.'.", parameterName);

            if (extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"File extension '{extension}' contains invalid characters.", parameterName);
        }

        private class TransformedSaveSerializer : ISaveSerializer
        {
            protected readonly ISaveSerializer Inner;
            protected readonly ISavePayloadTransform Transform;

            public TransformedSaveSerializer(ISaveSerializer inner, ISavePayloadTransform transform)
            {
                Inner = inner;
                Transform = transform;
            }

            public string FileExtension => Inner.FileExtension + Transform.FileExtensionSuffix;

            public ISaveSchematic CreateSchematic(Type stateType)
            {
                return Inner.CreateSchematic(stateType);
            }

            public byte[] Serialize(object data, ISaveSchematic schematic)
            {
                return Transform.Encode(Inner.Serialize(data, schematic));
            }

            public object Deserialize(byte[] rawData, ISaveSchematic schematic)
            {
                return Inner.Deserialize(Transform.Decode(rawData), schematic);
            }

            public int ExtractSchemaVersion(byte[] serializedData)
            {
                return Inner.ExtractSchemaVersion(Transform.Decode(serializedData));
            }
        }

        private sealed class MigrationCapableTransformedSaveSerializer :
            TransformedSaveSerializer,
            ISaveMigrationCapableSerializer
        {
            private readonly ISaveMigrationCapableSerializer _migrationCapableSerializer;

            public MigrationCapableTransformedSaveSerializer(
                ISaveSerializer inner,
                ISaveMigrationCapableSerializer migrationCapableSerializer,
                ISavePayloadTransform transform)
                : base(inner, transform)
            {
                _migrationCapableSerializer = migrationCapableSerializer;
            }

            public ISaveDataNodeFactory NodeFactory => _migrationCapableSerializer.NodeFactory;

            public ISaveDataNode DeserializeToNode(byte[] data)
            {
                return _migrationCapableSerializer.DeserializeToNode(Transform.Decode(data));
            }

            public byte[] SerializeFromNode(ISaveDataNode node)
            {
                return Transform.Encode(_migrationCapableSerializer.SerializeFromNode(node));
            }
        }

        private sealed class MetadataTransformedSaveSerializer :
            TransformedSaveSerializer,
            ISaveSerializerMetadataHandler
        {
            private readonly ISaveSerializerMetadataHandler _metadataHandler;

            public MetadataTransformedSaveSerializer(
                ISaveSerializer inner,
                ISaveSerializerMetadataHandler metadataHandler,
                ISavePayloadTransform transform)
                : base(inner, transform)
            {
                _metadataHandler = metadataHandler;
            }

            public void WriteMetadata(SaveSerializerMetadataWriteContext context)
            {
                _metadataHandler.WriteMetadata(context);
            }

            public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
            {
                _metadataHandler.ValidateMetadata(context);
            }
        }

        private sealed class MetadataMigrationCapableTransformedSaveSerializer :
            TransformedSaveSerializer,
            ISaveMigrationCapableSerializer,
            ISaveSerializerMetadataHandler
        {
            private readonly ISaveMigrationCapableSerializer _migrationCapableSerializer;
            private readonly ISaveSerializerMetadataHandler _metadataHandler;

            public MetadataMigrationCapableTransformedSaveSerializer(
                ISaveSerializer inner,
                ISaveMigrationCapableSerializer migrationCapableSerializer,
                ISaveSerializerMetadataHandler metadataHandler,
                ISavePayloadTransform transform)
                : base(inner, transform)
            {
                _migrationCapableSerializer = migrationCapableSerializer;
                _metadataHandler = metadataHandler;
            }

            public ISaveDataNodeFactory NodeFactory => _migrationCapableSerializer.NodeFactory;

            public ISaveDataNode DeserializeToNode(byte[] data)
            {
                return _migrationCapableSerializer.DeserializeToNode(Transform.Decode(data));
            }

            public byte[] SerializeFromNode(ISaveDataNode node)
            {
                return Transform.Encode(_migrationCapableSerializer.SerializeFromNode(node));
            }

            public void WriteMetadata(SaveSerializerMetadataWriteContext context)
            {
                _metadataHandler.WriteMetadata(context);
            }

            public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
            {
                _metadataHandler.ValidateMetadata(context);
            }
        }
    }
}
