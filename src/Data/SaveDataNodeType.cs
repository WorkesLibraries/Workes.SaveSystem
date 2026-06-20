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
        /// A floating-point number primitive type.
        /// </summary>
        Float,

        /// <summary>
        /// A string primitive type.
        /// </summary>
        String,

        /// <summary>
        /// A boolean primitive type.
        /// </summary>
        Bool,

        /// <summary>
        /// A null primitive type.
        /// </summary>
        Null
    }
}
