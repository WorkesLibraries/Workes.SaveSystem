using Newtonsoft.Json.Linq;
using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A serializer implementation that uses JSON format for save files.
    /// Creates <see cref="JsonSaveSchematic{T}"/> instances for each state type at registration.
    /// </summary>
    public sealed class JsonSaveSerializer : ISaveSerializer, ISaveMigrationCapableSerializer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSerializer"/> class.
        /// </summary>
        public JsonSaveSerializer()
        {
            NodeFactory = new JsonSaveDataNodeFactory();
        }

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
                var schematic = Activator.CreateInstance(schematicType);
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
        /// <returns>A JSON string representation of the data.</returns>
        public string Serialize(object data, ISaveSchematic schematic)
        {
            return schematic.SerializeUntyped(data);
        }

        /// <summary>
        /// Deserializes raw JSON data using the specified schematic. The schematic is responsible for the actual JSON deserialization.
        /// </summary>
        /// <param name="rawData">The raw JSON string to deserialize.</param>
        /// <param name="schematic">The schematic that defines how to deserialize the data.</param>
        /// <returns>The deserialized data object.</returns>
        public object Deserialize(string rawData, ISaveSchematic schematic)
        {
            return schematic.DeserializeUntyped(rawData);
        }

        /// <summary>
        /// Extracts the schema version from JSON serialized data by parsing the SchemaVersion field
        /// from the VersionedPayload structure without fully deserializing the data.
        /// </summary>
        /// <param name="serializedData">The serialized JSON string to extract the schema version from.</param>
        /// <returns>The schema version if found, or null if it cannot be determined.</returns>
        public int ExtractSchemaVersion(string serializedData)
        {
            try
            {
                var jsonObject = JObject.Parse(serializedData);
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
        public ISaveDataNode DeserializeToNode(string serializedData)
        {
            var root = JToken.Parse(serializedData);
            return new JsonSaveDataNode(root);
        }

        /// <inheritdoc />
        public string SerializeFromNode(ISaveDataNode node)
        {
            var jsonNode = (JsonSaveDataNode)node;
            return jsonNode._token.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateObject() => NodeFactory.CreateObject();

        /// <inheritdoc />
        public ISaveDataNode CreateArray() => NodeFactory.CreateArray();

        /// <inheritdoc />
        public ISaveDataNode CreateInt(int value) => NodeFactory.CreateInt(value);

        /// <inheritdoc />
        public ISaveDataNode CreateFloat(float value) => NodeFactory.CreateFloat(value);

        /// <inheritdoc />
        public ISaveDataNode CreateString(string value) => NodeFactory.CreateString(value);

        /// <inheritdoc />
        public ISaveDataNode CreateBool(bool value) => NodeFactory.CreateBool(value);
    }
}
