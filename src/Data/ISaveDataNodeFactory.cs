namespace Workes.SaveSystem
{
    /// <summary>
    /// Factory interface for creating save data nodes of various types.
    /// Used by migration-capable serializers to create nodes during migration operations.
    /// </summary>
    /// <remarks>
    /// Nodes should be combined only with other nodes created by the same serializer/factory implementation.
    /// </remarks>
    public interface ISaveDataNodeFactory
    {
        /// <summary>
        /// Creates a new object node (map/dictionary).
        /// </summary>
        /// <returns>A new empty object node.</returns>
        ISaveDataNode CreateObject();

        /// <summary>
        /// Creates a new array node (list).
        /// </summary>
        /// <returns>A new empty array node.</returns>
        ISaveDataNode CreateArray();

        /// <summary>
        /// Creates a new integer node with the specified value.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <returns>A new integer node.</returns>
        ISaveDataNode CreateInt(int value);

        /// <summary>
        /// Creates a new float node with the specified value.
        /// </summary>
        /// <param name="value">The float value.</param>
        /// <returns>A new float node.</returns>
        ISaveDataNode CreateFloat(float value);

        /// <summary>
        /// Creates a new string node with the specified value.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <returns>A new string node.</returns>
        ISaveDataNode CreateString(string value);

        /// <summary>
        /// Creates a new boolean node with the specified value.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <returns>A new boolean node.</returns>
        ISaveDataNode CreateBool(bool value);

        /// <summary>
        /// Creates a new null node.
        /// </summary>
        /// <returns>A new null node.</returns>
        ISaveDataNode CreateNull();
    }
}
