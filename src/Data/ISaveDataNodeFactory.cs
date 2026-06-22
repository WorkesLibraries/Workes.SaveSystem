namespace Workes.SaveSystem
{
    /// <summary>
    /// Factory interface for creating save data nodes of various types.
    /// Used by migration-capable serializers to create nodes during migration operations.
    /// </summary>
    /// <remarks>
    /// Nodes should be combined only with other nodes created by the same serializer/factory instance.
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
        /// Creates a new 64-bit integer node with the specified value.
        /// </summary>
        /// <param name="value">The 64-bit integer value.</param>
        /// <returns>A new 64-bit integer node.</returns>
        ISaveDataNode CreateLong(long value);

        /// <summary>
        /// Creates a new float node with the specified value.
        /// </summary>
        /// <param name="value">The float value.</param>
        /// <returns>A new float node.</returns>
        ISaveDataNode CreateFloat(float value);

        /// <summary>
        /// Creates a new double node with the specified value.
        /// </summary>
        /// <param name="value">The double value.</param>
        /// <returns>A new double node.</returns>
        ISaveDataNode CreateDouble(double value);

        /// <summary>
        /// Creates a new decimal node with the specified value.
        /// </summary>
        /// <param name="value">The decimal value.</param>
        /// <returns>A new decimal node.</returns>
        ISaveDataNode CreateDecimal(decimal value);

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
        /// Creates a new byte-array node with the specified value.
        /// </summary>
        /// <param name="value">The byte-array value.</param>
        /// <returns>A new byte-array node.</returns>
        ISaveDataNode CreateBytes(byte[] value);

        /// <summary>
        /// Creates a new date/time node with the specified value.
        /// </summary>
        /// <param name="value">The date/time value.</param>
        /// <returns>A new date/time node.</returns>
        ISaveDataNode CreateDateTime(System.DateTime value);

        /// <summary>
        /// Creates a new null node.
        /// </summary>
        /// <returns>A new null node.</returns>
        ISaveDataNode CreateNull();
    }
}
