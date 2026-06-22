namespace Workes.SaveSystem
{
    /// <summary>
    /// Optional migration API for provider payload formats that require save/provider context.
    /// </summary>
    public interface IContextualSaveMigrationCapableSerializer
    {
        /// <summary>
        /// Parses serialized provider data into an editable data-node tree using save/provider context.
        /// </summary>
        ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context);

        /// <summary>
        /// Serializes an edited data-node tree back to provider payload bytes using save/provider context.
        /// </summary>
        byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context);
    }
}
