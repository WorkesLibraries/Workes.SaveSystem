using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A serializer implementation that uses JSON format for save files.
    /// Creates <see cref="JsonSaveSchematic{T}"/> instances for each state type at registration.
    /// </summary>
    public sealed class JsonSaveSerializer : ISaveSerializer, ISaveMigrationCapableSerializer
    {
        private readonly SaveDataNodeFactory _nodeFactory;

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
        public object Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return schematic.DeserializeUntyped(rawData);
        }

        /// <summary>
        /// Extracts the schema version from JSON serialized data by parsing the SchemaVersion field
        /// from the VersionedPayload structure without fully deserializing the data.
        /// </summary>
        /// <param name="serializedData">The serialized UTF-8 JSON bytes to extract the schema version from.</param>
        /// <returns>The schema version if found, or null if it cannot be determined.</returns>
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
            var root = JToken.Parse(Encoding.UTF8.GetString(serializedData));
            return ConvertFromJson(root, _nodeFactory.Owner);
        }

        /// <inheritdoc />
        public byte[] SerializeFromNode(ISaveDataNode node)
        {
            var saveDataNode = SaveDataNode.RequireSaveDataNode(node, _nodeFactory.Owner);
            var json = ConvertToJson(saveDataNode).ToString(ToNewtonsoftFormatting(Formatting));
            return Encoding.UTF8.GetBytes(json);
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
                        return SaveDataNode.CreateInt(token.Value<int>(), owner);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("JSON integer value is outside the supported save data node range.", ex);
                    }

                case JTokenType.Float:
                    return SaveDataNode.CreateFloat(token.Value<float>(), owner);

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

                case SaveDataNodeType.Float:
                    return new JValue(node.AsFloat());

                case SaveDataNodeType.String:
                    return new JValue(node.AsString());

                case SaveDataNodeType.Bool:
                    return new JValue(node.AsBool());

                case SaveDataNodeType.Null:
                    return JValue.CreateNull();

                default:
                    throw new ArgumentOutOfRangeException(nameof(node));
            }
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
    }
}
