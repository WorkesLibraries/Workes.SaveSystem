using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A binary-token serializer for save files.
    /// </summary>
    /// <remarks>
    /// The current save serializer contract stores provider data as strings. This serializer writes a package-owned
    /// binary token payload encoded as Base64, preserving a binary payload format while staying compatible with the
    /// package's text-based persistence contract.
    /// </remarks>
    public sealed class BinarySaveSerializer : ISaveSerializer, ISaveMigrationCapableSerializer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinarySaveSerializer"/> class.
        /// </summary>
        public BinarySaveSerializer()
        {
            NodeFactory = new JsonSaveDataNodeFactory();
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
                        "Serialized binary data does not contain a valid integer 'SchemaVersion' field."
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
            return new JsonSaveDataNode(DeserializeTokenFromBase64(serializedData));
        }

        /// <inheritdoc />
        public string SerializeFromNode(ISaveDataNode node)
        {
            if (!(node is JsonSaveDataNode jsonNode))
                throw new InvalidOperationException("Binary data nodes can only be serialized from nodes created by this serializer.");

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
            return BinarySaveTokenCodec.Decode(serializedData);
        }

        private static string SerializeTokenToBase64(JToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            return BinarySaveTokenCodec.Encode(token);
        }
    }

    internal static class BinarySaveTokenCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("WSSB1");

        public static string Encode(JToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                WriteToken(writer, token);
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
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

            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var magic = reader.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length || !BytesEqual(magic, Magic))
                    throw new InvalidOperationException("Serialized binary save data has an invalid format header.");

                return ReadToken(reader);
            }
        }

        private static void WriteToken(BinaryWriter writer, JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    writer.Write((byte)1);
                    var obj = (JObject)token;
                    writer.Write(obj.Count);
                    foreach (var property in obj.Properties())
                    {
                        writer.Write(property.Name);
                        WriteToken(writer, property.Value);
                    }
                    return;

                case JTokenType.Array:
                    writer.Write((byte)2);
                    var array = (JArray)token;
                    writer.Write(array.Count);
                    foreach (var item in array)
                        WriteToken(writer, item);
                    return;

                case JTokenType.Integer:
                    writer.Write((byte)3);
                    writer.Write(token.Value<long>());
                    return;

                case JTokenType.Float:
                    writer.Write((byte)4);
                    writer.Write(token.Value<double>());
                    return;

                case JTokenType.String:
                    writer.Write((byte)5);
                    writer.Write(token.Value<string>() ?? string.Empty);
                    return;

                case JTokenType.Boolean:
                    writer.Write((byte)6);
                    writer.Write(token.Value<bool>());
                    return;

                case JTokenType.Null:
                case JTokenType.Undefined:
                    writer.Write((byte)7);
                    return;

                default:
                    writer.Write((byte)5);
                    writer.Write(token.ToString());
                    return;
            }
        }

        private static JToken ReadToken(BinaryReader reader)
        {
            var tokenType = reader.ReadByte();
            switch (tokenType)
            {
                case 1:
                    var obj = new JObject();
                    var propertyCount = reader.ReadInt32();
                    for (int i = 0; i < propertyCount; i++)
                    {
                        var propertyName = reader.ReadString();
                        obj[propertyName] = ReadToken(reader);
                    }
                    return obj;

                case 2:
                    var array = new JArray();
                    var itemCount = reader.ReadInt32();
                    for (int i = 0; i < itemCount; i++)
                        array.Add(ReadToken(reader));
                    return array;

                case 3:
                    return new JValue(reader.ReadInt64());

                case 4:
                    return new JValue(reader.ReadDouble());

                case 5:
                    return new JValue(reader.ReadString());

                case 6:
                    return new JValue(reader.ReadBoolean());

                case 7:
                    return JValue.CreateNull();

                default:
                    throw new InvalidOperationException($"Serialized binary save data contains an unknown token type '{tokenType}'.");
            }
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }
    }
}
