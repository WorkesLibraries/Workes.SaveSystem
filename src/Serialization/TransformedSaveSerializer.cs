using System;
using System.IO;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Decorates another serializer with a reversible byte transform.
    /// </summary>
    /// <remarks>
    /// Use this for format-level concerns such as custom obfuscation or encryption.
    /// </remarks>
    public sealed class TransformedSaveSerializer : ISaveSerializer, IContextualSaveSerializer, ISaveApplicationMetadataSerializer
    {
        private readonly ISaveMigrationCapableSerializer? _migration;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformedSaveSerializer"/> class.
        /// </summary>
        /// <param name="inner">The serializer that produces and consumes untransformed payload bytes.</param>
        /// <param name="transform">The transform that encodes and decodes payload bytes.</param>
        public TransformedSaveSerializer(ISaveSerializer inner, ISavePayloadTransform transform)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Transform = transform ?? throw new ArgumentNullException(nameof(transform));

            ValidateExtension(Inner.FileExtension, nameof(inner));
            ValidateExtension(Transform.FileExtensionSuffix, nameof(transform));

            if (Inner.Migration != null)
                _migration = new TransformedMigrationAdapter(Inner.Migration, Transform);
        }

        /// <summary>
        /// Gets the inner serializer.
        /// </summary>
        public ISaveSerializer Inner { get; }

        /// <summary>
        /// Gets the payload transform.
        /// </summary>
        public ISavePayloadTransform Transform { get; }

        /// <inheritdoc />
        public string FileExtension => Inner.FileExtension + Transform.FileExtensionSuffix;

        /// <inheritdoc />
        public ISaveMigrationCapableSerializer? Migration => _migration;

        /// <inheritdoc />
        public ISaveSerializerMetadataHandler? Metadata => Inner.Metadata;

        /// <inheritdoc />
        public ISaveSchematic CreateSchematic(Type stateType)
        {
            return Inner.CreateSchematic(stateType);
        }

        /// <inheritdoc />
        public byte[] Serialize(object data, ISaveSchematic schematic)
        {
            return Transform.Encode(Inner.Serialize(data, schematic));
        }

        /// <inheritdoc />
        public byte[] Serialize(object data, SaveSerializerContext context)
        {
            var innerData = Inner is IContextualSaveSerializer contextual
                ? contextual.Serialize(data, context)
                : Inner.Serialize(data, context.Schematic);

            return Transform.Encode(innerData);
        }

        /// <inheritdoc />
        public object? Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return Inner.Deserialize(Transform.Decode(rawData), schematic);
        }

        /// <inheritdoc />
        public object? Deserialize(byte[] rawData, SaveSerializerContext context)
        {
            var decoded = Transform.Decode(rawData);
            return Inner is IContextualSaveSerializer contextual
                ? contextual.Deserialize(decoded, context)
                : Inner.Deserialize(decoded, context.Schematic);
        }

        /// <inheritdoc />
        public int ExtractSchemaVersion(byte[] serializedData)
        {
            return Inner.ExtractSchemaVersion(Transform.Decode(serializedData));
        }

        /// <inheritdoc />
        public int ExtractSchemaVersion(byte[] serializedData, SaveSerializerContext context)
        {
            var decoded = Transform.Decode(serializedData);
            return Inner is IContextualSaveSerializer contextual
                ? contextual.ExtractSchemaVersion(decoded, context)
                : Inner.ExtractSchemaVersion(decoded);
        }

        /// <inheritdoc />
        public object? SerializeApplicationMetadata(object? metadata, SaveSerializerContext context)
        {
            return RequireApplicationMetadataSerializer().SerializeApplicationMetadata(metadata, context);
        }

        /// <inheritdoc />
        public object? DeserializeApplicationMetadata(object? data, SaveSerializerContext context)
        {
            return RequireApplicationMetadataSerializer().DeserializeApplicationMetadata(data, context);
        }

        /// <inheritdoc />
        public ISaveDataNode DeserializeApplicationMetadataToNode(object? data, SaveSerializerContext context)
        {
            return RequireApplicationMetadataSerializer().DeserializeApplicationMetadataToNode(data, context);
        }

        /// <inheritdoc />
        public object? SerializeApplicationMetadataFromNode(ISaveDataNode node, SaveSerializerContext context)
        {
            return RequireApplicationMetadataSerializer().SerializeApplicationMetadataFromNode(node, context);
        }

        private ISaveApplicationMetadataSerializer RequireApplicationMetadataSerializer()
        {
            if (Inner is ISaveApplicationMetadataSerializer applicationMetadataSerializer)
                return applicationMetadataSerializer;

            throw new InvalidOperationException(
                $"Inner serializer '{Inner.GetType().Name}' does not implement {nameof(ISaveApplicationMetadataSerializer)}.");
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

        private sealed class TransformedMigrationAdapter : ISaveMigrationCapableSerializer, IContextualSaveMigrationCapableSerializer
        {
            private readonly ISaveMigrationCapableSerializer _inner;
            private readonly ISavePayloadTransform _transform;

            public TransformedMigrationAdapter(
                ISaveMigrationCapableSerializer inner,
                ISavePayloadTransform transform)
            {
                _inner = inner;
                _transform = transform;
            }

            public ISaveDataNodeFactory NodeFactory => _inner.NodeFactory;

            public ISaveDataNode DeserializeToNode(byte[] data)
            {
                return _inner.DeserializeToNode(_transform.Decode(data));
            }

            public ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context)
            {
                var decoded = _transform.Decode(data);
                return _inner is IContextualSaveMigrationCapableSerializer contextual
                    ? contextual.DeserializeToNode(decoded, context)
                    : _inner.DeserializeToNode(decoded);
            }

            public byte[] SerializeFromNode(ISaveDataNode node)
            {
                return _transform.Encode(_inner.SerializeFromNode(node));
            }

            public byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context)
            {
                var innerData = _inner is IContextualSaveMigrationCapableSerializer contextual
                    ? contextual.SerializeFromNode(node, context)
                    : _inner.SerializeFromNode(node);

                return _transform.Encode(innerData);
            }
        }
    }
}
