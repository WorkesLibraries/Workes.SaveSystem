using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A serializer implementation that uses JSON format for save files.
    /// Creates <see cref="JsonSaveSchematic{T}"/> instances for each state type at registration.
    /// </summary>
    public sealed class JsonSaveSerializer :
        ISaveSerializer,
        ISaveMigrationCapableSerializer,
        IContextualSaveMigrationCapableSerializer,
        ISaveApplicationMetadataSerializer
    {
        private const int DefaultMigrationSchemaVersion = 1;
        private readonly SaveDataNodeFactory _nodeFactory;
        private readonly ConditionalWeakTable<ISaveDataNode, VersionHolder> _nodeVersions =
            new ConditionalWeakTable<ISaveDataNode, VersionHolder>();

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSerializer"/> class.
        /// </summary>
        public JsonSaveSerializer()
            : this(JsonSaveFormatting.Pretty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSerializer"/> class.
        /// </summary>
        /// <param name="formatting">The JSON formatting style to use when writing save payloads.</param>
        public JsonSaveSerializer(JsonSaveFormatting formatting)
        {
            if (!Enum.IsDefined(typeof(JsonSaveFormatting), formatting))
                throw new ArgumentOutOfRangeException(nameof(formatting));

            Formatting = formatting;
            _nodeFactory = new SaveDataNodeFactory();
            NodeFactory = _nodeFactory;
        }

        /// <summary>
        /// Gets the JSON formatting style used when writing save payloads.
        /// </summary>
        public JsonSaveFormatting Formatting { get; }

        /// <summary>
        /// Creates a <see cref="JsonSaveSchematic{T}"/> for the given state type.
        /// </summary>
        public ISaveSchematic CreateSchematic(Type stateType)
        {
            if (stateType == null)
                throw new ArgumentNullException(nameof(stateType));

            var schematicType = typeof(JsonSaveSchematic<>).MakeGenericType(stateType);
            try
            {
                var schematic = Activator.CreateInstance(schematicType, Formatting);
                if (schematic == null)
                    throw new ArgumentException($"JsonSaveSerializer could not create a schematic for type {stateType.Name}.", nameof(stateType));

                return (ISaveSchematic)schematic;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"JsonSaveSerializer cannot create a schematic for type {stateType.Name}.",
                    nameof(stateType),
                    ex
                );
            }
        }

        /// <summary>
        /// Gets the file extension used for JSON save files: ".json".
        /// </summary>
        public string FileExtension => ".json";

        /// <inheritdoc />
        public ISaveMigrationCapableSerializer Migration => this;

        /// <inheritdoc />
        public ISaveSerializerMetadataHandler? Metadata => null;

        /// <inheritdoc />
        public ISaveDataNodeFactory NodeFactory { get; }

        /// <summary>
        /// Serializes data using the specified schematic. The schematic is responsible for the actual JSON serialization.
        /// </summary>
        /// <param name="data">The data object to serialize.</param>
        /// <param name="schematic">The schematic that defines how to serialize the data.</param>
        /// <returns>UTF-8 JSON bytes.</returns>
        public byte[] Serialize(object data, ISaveSchematic schematic)
        {
            return schematic.SerializeUntyped(data);
        }

        /// <summary>
        /// Deserializes raw JSON data using the specified schematic. The schematic is responsible for the actual JSON deserialization.
        /// </summary>
        /// <param name="rawData">The raw UTF-8 JSON bytes to deserialize.</param>
        /// <param name="schematic">The schematic that defines how to deserialize the data.</param>
        /// <returns>The deserialized data object.</returns>
        public object? Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return schematic.DeserializeUntyped(rawData);
        }

        /// <summary>
        /// Extracts the schema version from JSON serialized data by parsing the SchemaVersion field
        /// from the VersionedPayload structure without fully deserializing the data.
        /// </summary>
        /// <param name="serializedData">The serialized UTF-8 JSON bytes to extract the schema version from.</param>
        /// <returns>The schema version extracted from the serialized data.</returns>
        public int ExtractSchemaVersion(byte[] serializedData)
        {
            try
            {
                var jsonObject = JObject.Parse(Encoding.UTF8.GetString(serializedData));
                var schemaVersionToken = jsonObject["SchemaVersion"];

                if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
                {
                    throw new InvalidOperationException(
                        "Serialized JSON does not contain a valid integer 'SchemaVersion' field."
                    );
                }

                return schemaVersionToken.Value<int>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to extract schema version from JSON save data.",
                    ex
                );
            }
        }
        
        /// <inheritdoc />
        public ISaveDataNode DeserializeToNode(byte[] serializedData)
        {
            var envelope = ReadEnvelope(serializedData);
            var dataNode = ConvertFromJson(envelope.Data, _nodeFactory.Owner);
            _nodeVersions.Remove(dataNode);
            _nodeVersions.Add(dataNode, new VersionHolder(envelope.SchemaVersion));
            return dataNode;
        }

        /// <inheritdoc />
        public byte[] SerializeFromNode(ISaveDataNode node)
        {
            var schemaVersion = _nodeVersions.TryGetValue(node, out var holder)
                ? holder.SchemaVersion
                : DefaultMigrationSchemaVersion;

            return SerializeEnvelopeFromNode(node, schemaVersion);
        }

        /// <inheritdoc />
        public ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return DeserializeToNode(data);
        }

        /// <inheritdoc />
        public byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return SerializeEnvelopeFromNode(node, context.SchemaVersion);
        }

        /// <inheritdoc />
        public object? SerializeApplicationMetadata(object? metadata, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var serialized = context.Schematic.SerializeUntyped(metadata!);
            return ReadEnvelope(serialized).Data.DeepClone();
        }

        /// <inheritdoc />
        public object? DeserializeApplicationMetadata(object? data, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var envelope = new JObject
            {
                ["SchemaVersion"] = context.SchemaVersion,
                ["Data"] = ToJToken(data)
            };
            var json = envelope.ToString(ToNewtonsoftFormatting(Formatting));
            return context.Schematic.DeserializeUntyped(Encoding.UTF8.GetBytes(json));
        }

        /// <inheritdoc />
        public ISaveDataNode DeserializeApplicationMetadataToNode(object? data, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return ConvertFromJson(ToJToken(data), _nodeFactory.Owner);
        }

        /// <inheritdoc />
        public object? SerializeApplicationMetadataFromNode(ISaveDataNode node, SaveSerializerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var saveDataNode = SaveDataNode.RequireSaveDataNode(node, _nodeFactory.Owner);
            return ConvertToJson(saveDataNode);
        }

        private byte[] SerializeEnvelopeFromNode(ISaveDataNode node, int schemaVersion)
        {
            var saveDataNode = SaveDataNode.RequireSaveDataNode(node, _nodeFactory.Owner);
            var envelope = new JObject
            {
                ["SchemaVersion"] = schemaVersion,
                ["Data"] = ConvertToJson(saveDataNode)
            };
            var json = envelope.ToString(ToNewtonsoftFormatting(Formatting));
            return Encoding.UTF8.GetBytes(json);
        }

        private static JsonEnvelope ReadEnvelope(byte[] serializedData)
        {
            if (serializedData == null)
                throw new ArgumentNullException(nameof(serializedData));

            using var stringReader = new StringReader(Encoding.UTF8.GetString(serializedData));
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None
            };

            var root = JToken.ReadFrom(jsonReader);
            if (!(root is JObject jsonObject))
                throw new InvalidOperationException("JSON save payload root must be an object envelope.");

            var schemaVersionToken = jsonObject["SchemaVersion"];
            if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
            {
                throw new InvalidOperationException(
                    "Serialized JSON does not contain a valid integer 'SchemaVersion' field.");
            }

            if (!jsonObject.TryGetValue("Data", out var dataToken))
                throw new InvalidOperationException("Serialized JSON does not contain a 'Data' field.");

            return new JsonEnvelope(schemaVersionToken.Value<int>(), dataToken);
        }

        private static SaveDataNode ConvertFromJson(JToken token, object owner)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var objectNode = SaveDataNode.CreateObject(owner);
                    foreach (var property in ((JObject)token).Properties())
                    {
                        objectNode.Set(property.Name, ConvertFromJson(property.Value, owner));
                    }

                    return objectNode;

                case JTokenType.Array:
                    var arrayNode = SaveDataNode.CreateArray(owner);
                    foreach (var child in (JArray)token)
                    {
                        arrayNode.Add(ConvertFromJson(child, owner));
                    }

                    return arrayNode;

                case JTokenType.Integer:
                    try
                    {
                        var longValue = token.Value<long>();
                        if (longValue >= int.MinValue && longValue <= int.MaxValue)
                            return SaveDataNode.CreateInt((int)longValue, owner);

                        return SaveDataNode.CreateLong(longValue, owner);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("JSON integer value is outside the supported save data node range.", ex);
                    }

                case JTokenType.Float:
                    return SaveDataNode.CreateDouble(token.Value<double>(), owner);

                case JTokenType.String:
                    var stringValue = token.Value<string>();
                    if (stringValue == null)
                        throw new InvalidOperationException("JSON string token did not contain a string value.");

                    return SaveDataNode.CreateString(stringValue, owner);

                case JTokenType.Boolean:
                    return SaveDataNode.CreateBool(token.Value<bool>(), owner);

                case JTokenType.Null:
                    return SaveDataNode.CreateNull(owner);

                default:
                    throw new InvalidOperationException(
                        $"JSON token type '{token.Type}' cannot be represented by the save data node migration model."
                    );
            }
        }

        private static JToken ConvertToJson(SaveDataNode node)
        {
            switch (node.NodeType)
            {
                case SaveDataNodeType.Object:
                    var obj = new JObject();
                    foreach (var key in node.Keys)
                    {
                        obj[key] = ConvertToJson((SaveDataNode)node.Get(key));
                    }

                    return obj;

                case SaveDataNodeType.Array:
                    var array = new JArray();
                    for (var i = 0; i < node.Count; i++)
                    {
                        array.Add(ConvertToJson((SaveDataNode)node.GetAt(i)));
                    }

                    return array;

                case SaveDataNodeType.Int:
                    return new JValue(node.AsInt());

                case SaveDataNodeType.Long:
                    return new JValue(node.AsLong());

                case SaveDataNodeType.Float:
                    return new JValue(node.AsFloat());

                case SaveDataNodeType.Double:
                    return new JValue(node.AsDouble());

                case SaveDataNodeType.Decimal:
                    return new JValue(node.AsDecimal().ToString(CultureInfo.InvariantCulture));

                case SaveDataNodeType.String:
                    return new JValue(node.AsString());

                case SaveDataNodeType.Bool:
                    return new JValue(node.AsBool());

                case SaveDataNodeType.Bytes:
                    return new JValue(Convert.ToBase64String(node.AsBytes()));

                case SaveDataNodeType.DateTime:
                    return new JValue(node.AsDateTime().ToString("O", CultureInfo.InvariantCulture));

                case SaveDataNodeType.Null:
                    return JValue.CreateNull();

                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
        }

        private static JToken ToJToken(object? data)
        {
            if (data == null)
                return JValue.CreateNull();

            if (data is JToken token)
                return token.DeepClone();

            return JToken.FromObject(data);
        }

        internal static Newtonsoft.Json.Formatting ToNewtonsoftFormatting(JsonSaveFormatting formatting)
        {
            switch (formatting)
            {
                case JsonSaveFormatting.Pretty:
                    return Newtonsoft.Json.Formatting.Indented;
                case JsonSaveFormatting.Compact:
                    return Newtonsoft.Json.Formatting.None;
                default:
                    throw new ArgumentOutOfRangeException(nameof(formatting));
            }
        }

        private sealed class VersionHolder
        {
            public VersionHolder(int schemaVersion)
            {
                SchemaVersion = schemaVersion;
            }

            public int SchemaVersion { get; }
        }

        private readonly struct JsonEnvelope
        {
            public JsonEnvelope(int schemaVersion, JToken data)
            {
                SchemaVersion = schemaVersion;
                Data = data;
            }

            public int SchemaVersion { get; }

            public JToken Data { get; }
        }
    }
}
