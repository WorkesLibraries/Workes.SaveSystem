namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents the type of a save data node. Used for type safety when working with save data.
    /// </summary>
    public enum SaveDataNodeType
    {
        /// <summary>
        /// A JSON object / map / dictionary type.
        /// </summary>
        Object,

        /// <summary>
        /// An array / list type.
        /// </summary>
        Array,

        /// <summary>
        /// An integer primitive type.
        /// </summary>
        Int,

        /// <summary>
        /// A 64-bit integer primitive type.
        /// </summary>
        Long,

        /// <summary>
        /// A floating-point number primitive type.
        /// </summary>
        Float,

        /// <summary>
        /// A double-precision floating-point number primitive type.
        /// </summary>
        Double,

        /// <summary>
        /// A decimal primitive type.
        /// </summary>
        Decimal,

        /// <summary>
        /// A string primitive type.
        /// </summary>
        String,

        /// <summary>
        /// A boolean primitive type.
        /// </summary>
        Bool,

        /// <summary>
        /// A byte-array primitive type.
        /// </summary>
        Bytes,

        /// <summary>
        /// A date/time primitive type.
        /// </summary>
        DateTime,

        /// <summary>
        /// A null primitive type.
        /// </summary>
        Null
    }
}
