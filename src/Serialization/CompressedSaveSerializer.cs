using System;
using System.IO;
using System.IO.Compression;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Decorates another serializer with GZip compression.
    /// </summary>
    public sealed class CompressedSaveSerializer : ISaveSerializer
    {
        private readonly TransformedSaveSerializer _transformed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressedSaveSerializer"/> class.
        /// </summary>
        /// <param name="inner">The serializer whose payloads should be compressed.</param>
        public CompressedSaveSerializer(ISaveSerializer inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _transformed = new TransformedSaveSerializer(Inner, new GZipPayloadTransform());
        }

        /// <summary>
        /// Gets the inner serializer.
        /// </summary>
        public ISaveSerializer Inner { get; }

        /// <inheritdoc />
        public string FileExtension => _transformed.FileExtension;

        /// <inheritdoc />
        public ISaveMigrationCapableSerializer? Migration => _transformed.Migration;

        /// <inheritdoc />
        public ISaveSerializerMetadataHandler? Metadata => _transformed.Metadata;

        /// <inheritdoc />
        public ISaveSchematic CreateSchematic(Type stateType)
        {
            return _transformed.CreateSchematic(stateType);
        }

        /// <inheritdoc />
        public byte[] Serialize(object data, ISaveSchematic schematic)
        {
            return _transformed.Serialize(data, schematic);
        }

        /// <inheritdoc />
        public object Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return _transformed.Deserialize(rawData, schematic);
        }

        /// <inheritdoc />
        public int ExtractSchemaVersion(byte[] serializedData)
        {
            return _transformed.ExtractSchemaVersion(serializedData);
        }

        private sealed class GZipPayloadTransform : ISavePayloadTransform
        {
            public string FileExtensionSuffix => ".gz";

            public byte[] Encode(byte[] data)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                using var output = new MemoryStream();
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    gzip.Write(data, 0, data.Length);
                }

                return output.ToArray();
            }

            public byte[] Decode(byte[] data)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                using var input = new MemoryStream(data);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
