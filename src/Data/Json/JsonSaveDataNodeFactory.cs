using Newtonsoft.Json.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Factory for creating JSON-backed save data nodes.
    /// Produces nodes backed by Newtonsoft JSON tokens.
    /// </summary>
    public sealed class JsonSaveDataNodeFactory : ISaveDataNodeFactory
    {
        /// <inheritdoc />
        public ISaveDataNode CreateObject()
        {
            return new JsonSaveDataNode(new JObject());
        }

        /// <inheritdoc />
        public ISaveDataNode CreateArray()
        {
            return new JsonSaveDataNode(new JArray());
        }

        /// <inheritdoc />
        public ISaveDataNode CreateInt(int value)
        {
            return new JsonSaveDataNode(new JValue(value));
        }

        /// <inheritdoc />
        public ISaveDataNode CreateFloat(float value)
        {
            return new JsonSaveDataNode(new JValue(value));
        }

        /// <inheritdoc />
        public ISaveDataNode CreateString(string value)
        {
            return new JsonSaveDataNode(new JValue(value));
        }

        /// <inheritdoc />
        public ISaveDataNode CreateBool(bool value)
        {
            return new JsonSaveDataNode(new JValue(value));
        }

        /// <inheritdoc />
        public ISaveDataNode CreateNull()
        {
            return new JsonSaveDataNode(JValue.CreateNull());
        }
    }
}
