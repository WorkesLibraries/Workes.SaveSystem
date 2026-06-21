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
        private readonly JsonSaveDataNodeFactory _nodeFactory;

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
            _nodeFactory = new JsonSaveDataNodeFactory();
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
            return new JsonSaveDataNode(root, _nodeFactory.Owner);
        }

        /// <inheritdoc />
        public byte[] SerializeFromNode(ISaveDataNode node)
        {
            var jsonNode = JsonSaveDataNode.RequireJsonNode(node, _nodeFactory.Owner);
            var json = jsonNode._token.ToString(ToNewtonsoftFormatting(Formatting));
            return Encoding.UTF8.GetBytes(json);
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
