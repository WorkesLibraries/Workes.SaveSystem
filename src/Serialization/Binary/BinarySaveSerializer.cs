using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A Base64 JSON-token serializer for save files.
    /// </summary>
    /// <remarks>
    /// The current save serializer contract stores provider data as strings. This serializer writes a Base64-encoded
    /// UTF-8 JSON token payload, so decoding the file content with ordinary Base64 tools produces readable structured
    /// JSON while preserving a distinct serializer/file extension.
    /// </remarks>
    public sealed class BinarySaveSerializer : ISaveSerializer, ISaveMigrationCapableSerializer
    {
        private readonly JsonSaveDataNodeFactory _nodeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinarySaveSerializer"/> class.
        /// </summary>
        public BinarySaveSerializer()
        {
            _nodeFactory = new JsonSaveDataNodeFactory();
            NodeFactory = _nodeFactory;
        }

        /// <summary>
        /// Gets the file extension used for binary save files: ".bin".
        /// </summary>
        public string FileExtension => ".bin";

        /// <inheritdoc />
        public ISaveDataNodeFactory NodeFactory { get; }

        /// <summary>
        /// Creates a <see cref="BinarySaveSchematic{T}"/> for the given state type.
        /// </summary>
        public ISaveSchematic CreateSchematic(Type stateType)
        {
            if (stateType == null)
                throw new ArgumentNullException(nameof(stateType));

            var schematicType = typeof(BinarySaveSchematic<>).MakeGenericType(stateType);
            try
            {
                var schematic = Activator.CreateInstance(schematicType);
                if (schematic == null)
                    throw new ArgumentException($"BinarySaveSerializer could not create a schematic for type {stateType.Name}.", nameof(stateType));

                return (ISaveSchematic)schematic;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"BinarySaveSerializer cannot create a schematic for type {stateType.Name}.",
                    nameof(stateType),
                    ex
                );
            }
        }

        /// <inheritdoc />
        public string Serialize(object data, ISaveSchematic schematic)
        {
            return schematic.SerializeUntyped(data);
        }

        /// <inheritdoc />
        public object Deserialize(string rawData, ISaveSchematic schematic)
        {
            return schematic.DeserializeUntyped(rawData);
        }

        /// <inheritdoc />
        public int ExtractSchemaVersion(string serializedData)
        {
            try
            {
                var root = DeserializeTokenFromBase64(serializedData);
                var schemaVersionToken = root["SchemaVersion"];
                if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
                {
                    throw new InvalidOperationException(
                        "Serialized binary save data does not contain a valid integer 'SchemaVersion' field."
                    );
                }

                return schemaVersionToken.Value<int>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to extract schema version from binary save data.",
                    ex
                );
            }
        }

        /// <inheritdoc />
        public ISaveDataNode DeserializeToNode(string serializedData)
        {
            return new JsonSaveDataNode(DeserializeTokenFromBase64(serializedData), _nodeFactory.Owner);
        }

        /// <inheritdoc />
        public string SerializeFromNode(ISaveDataNode node)
        {
            var jsonNode = JsonSaveDataNode.RequireJsonNode(node, _nodeFactory.Owner);

            return SerializeTokenToBase64(jsonNode._token);
        }

        internal static string SerializePayloadToBase64(object payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var token = JToken.FromObject(payload, JsonSerializer.CreateDefault());
            return SerializeTokenToBase64(token);
        }

        internal static T? DeserializePayloadFromBase64<T>(string serializedData)
        {
            return DeserializeTokenFromBase64(serializedData).ToObject<T>(JsonSerializer.CreateDefault());
        }

        private static JToken DeserializeTokenFromBase64(string serializedData)
        {
            return BinarySaveBase64JsonCodec.Decode(serializedData);
        }

        private static string SerializeTokenToBase64(JToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            return BinarySaveBase64JsonCodec.Encode(token);
        }
    }

    internal static class BinarySaveBase64JsonCodec
    {
        public static string Encode(JToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            var json = token.ToString(Newtonsoft.Json.Formatting.Indented);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public static JToken Decode(string serializedData)
        {
            if (string.IsNullOrWhiteSpace(serializedData))
                throw new InvalidOperationException("Serialized binary save data cannot be null, empty, or whitespace.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(serializedData);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Serialized binary save data is not valid Base64.", ex);
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                return JToken.Parse(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Serialized binary save data does not contain readable JSON after Base64 decoding.", ex);
            }
        }
    }
}
