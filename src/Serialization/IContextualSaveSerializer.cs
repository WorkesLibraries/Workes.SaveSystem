using System.Diagnostics.CodeAnalysis;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional serializer API for provider payload formats that require save/provider context.
    /// </summary>
    public interface IContextualSaveSerializer
    {
        /// <summary>
        /// Serializes provider data using save/provider context.
        /// </summary>
        byte[] Serialize(object data, SaveSerializerContext context);

        /// <summary>
        /// Deserializes provider data using save/provider context.
        /// </summary>
        [return: MaybeNull]
        object Deserialize(byte[] rawData, SaveSerializerContext context);

        /// <summary>
        /// Extracts the schema version from serialized provider data using save/provider context.
        /// </summary>
        int ExtractSchemaVersion(byte[] serializedData, SaveSerializerContext context);
    }
}
