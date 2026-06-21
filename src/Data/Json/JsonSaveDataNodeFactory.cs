using Newtonsoft.Json.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Factory for creating JSON-backed save data nodes.
    /// Produces nodes backed by Newtonsoft JSON tokens.
    /// </summary>
    internal sealed class JsonSaveDataNodeFactory : ISaveDataNodeFactory
    {
        internal object Owner { get; } = new object();

        /// <inheritdoc />
        public ISaveDataNode CreateObject()
        {
            return new JsonSaveDataNode(new JObject(), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateArray()
        {
            return new JsonSaveDataNode(new JArray(), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateInt(int value)
        {
            return new JsonSaveDataNode(new JValue(value), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateFloat(float value)
        {
            return new JsonSaveDataNode(new JValue(value), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateString(string value)
        {
            return new JsonSaveDataNode(new JValue(value), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateBool(bool value)
        {
            return new JsonSaveDataNode(new JValue(value), Owner);
        }

        /// <inheritdoc />
        public ISaveDataNode CreateNull()
        {
            return new JsonSaveDataNode(JValue.CreateNull(), Owner);
        }
    }
}
