namespace Workes.SaveSystem
{
    /// <summary>
    /// Defines serializer operations required to migrate serialized save data without fully deserializing it.
    /// </summary>
    /// <remarks>
    /// Migration-capable serializers own the concrete <see cref="ISaveDataNode"/> trees they produce and expose
    /// the matching node factory through <see cref="NodeFactory"/>. Migration steps should only combine nodes
    /// created by the same serializer/factory instance.
    /// </remarks>
    public interface ISaveMigrationCapableSerializer
    {
        /// <summary>
        /// Parses serialized data into an editable data-node tree.
        /// </summary>
        /// <param name="data">The serialized payload to parse.</param>
        /// <returns>The root data node.</returns>
        ISaveDataNode DeserializeToNode(string data);

        /// <summary>
        /// Serializes an edited data-node tree back to the serializer's payload format.
        /// </summary>
        /// <param name="node">The root data node to serialize.</param>
        /// <returns>The serialized payload.</returns>
        string SerializeFromNode(ISaveDataNode node);

        /// <summary>
        /// Gets the factory used to create new nodes during migration.
        /// </summary>
        ISaveDataNodeFactory NodeFactory { get; }
    }
}
