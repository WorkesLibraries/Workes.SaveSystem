using System.Collections.Generic;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Provides a format-neutral editable view over serialized save data during migrations.
    /// </summary>
    /// <remarks>
    /// Migration steps use this contract to inspect and mutate persisted payload data without depending
    /// on the serializer's native object model. Nodes are owned by the serializer/factory instance that
    /// created them and should not be mixed with nodes from another instance.
    /// </remarks>
    public interface ISaveDataNode
    {
        /// <summary>
        /// Gets the type of this node. Used for type safety when working with save data.
        /// </summary>
        SaveDataNodeType NodeType { get; }

        /// <summary>
        /// Returns whether this node represents an object/map value.
        /// </summary>
        bool IsObject();

        /// <summary>
        /// Returns whether this node represents an array value.
        /// </summary>
        bool IsArray();

        /// <summary>
        /// Returns whether this node represents a null value.
        /// </summary>
        bool IsNull();

        /// <summary>
        /// Gets the number of child entries for object and array nodes.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the array child at the specified index.
        /// </summary>
        /// <param name="index">The zero-based array index.</param>
        /// <returns>The child node at <paramref name="index"/>.</returns>
        ISaveDataNode GetAt(int index);

        /// <summary>
        /// Replaces the array child at the specified index.
        /// </summary>
        /// <param name="index">The zero-based array index.</param>
        /// <param name="value">The replacement node.</param>
        void SetAt(int index, ISaveDataNode value);

        /// <summary>
        /// Inserts a node into an array.
        /// </summary>
        /// <param name="index">The zero-based insertion index.</param>
        /// <param name="value">The node to insert.</param>
        /// <returns>True when the node was inserted; otherwise, false.</returns>
        bool InsertAt(int index, ISaveDataNode value);

        /// <summary>
        /// Removes an array child at the specified index.
        /// </summary>
        /// <param name="index">The zero-based array index.</param>
        /// <returns>True when a node was removed; otherwise, false.</returns>
        bool RemoveAt(int index);

        /// <summary>
        /// Appends a node to an array.
        /// </summary>
        /// <param name="value">The node to append.</param>
        void Add(ISaveDataNode value);

        /// <summary>
        /// Returns whether an object node contains the specified key.
        /// </summary>
        /// <param name="key">The object key to check.</param>
        /// <returns>True when the key exists; otherwise, false.</returns>
        bool Has(string key);

        /// <summary>
        /// Gets an object child by key.
        /// </summary>
        /// <param name="key">The object key to read.</param>
        /// <returns>The child node for <paramref name="key"/>.</returns>
        ISaveDataNode Get(string key);

        /// <summary>
        /// Sets an object child by key.
        /// </summary>
        /// <param name="key">The object key to write.</param>
        /// <param name="value">The node to store.</param>
        void Set(string key, ISaveDataNode value);

        /// <summary>
        /// Removes an object child by key.
        /// </summary>
        /// <param name="key">The object key to remove.</param>
        /// <returns>True when a node was removed; otherwise, false.</returns>
        bool Remove(string key);

        /// <summary>
        /// Reads this node as an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        int AsInt();

        /// <summary>
        /// Replaces this node with an integer value.
        /// </summary>
        /// <param name="value">The integer value to write.</param>
        void SetInt(int value);

        /// <summary>
        /// Reads this node as a floating-point value.
        /// </summary>
        /// <returns>The floating-point value.</returns>
        float AsFloat();

        /// <summary>
        /// Replaces this node with a floating-point value.
        /// </summary>
        /// <param name="value">The floating-point value to write.</param>
        void SetFloat(float value);

        /// <summary>
        /// Reads this node as a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        string AsString();

        /// <summary>
        /// Replaces this node with a string value.
        /// </summary>
        /// <param name="value">The string value to write.</param>
        void SetString(string value);

        /// <summary>
        /// Reads this node as a Boolean value.
        /// </summary>
        /// <returns>The Boolean value.</returns>
        bool AsBool();

        /// <summary>
        /// Replaces this node with a Boolean value.
        /// </summary>
        /// <param name="value">The Boolean value to write.</param>
        void SetBool(bool value);

        /// <summary>
        /// Replaces this node with a null value.
        /// </summary>
        void SetNull();

        /// <summary>
        /// Gets the object keys for object nodes.
        /// </summary>
        IEnumerable<string> Keys { get; }
    }
}
