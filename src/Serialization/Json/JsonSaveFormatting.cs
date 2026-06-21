namespace Workes.SaveSystem
{
    /// <summary>
    /// Controls how JSON save payloads are formatted on disk.
    /// </summary>
    public enum JsonSaveFormatting
    {
        /// <summary>
        /// Writes indented, human-readable JSON.
        /// </summary>
        Pretty,

        /// <summary>
        /// Writes JSON without indentation or extra formatting whitespace.
        /// </summary>
        Compact
    }
}
