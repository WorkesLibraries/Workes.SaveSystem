using System.Collections.Generic;

namespace Workes.SaveSystem
{
    internal sealed class SerializedSnapshot
    {
        internal sealed class SerializedEntry
        {
            public string Data = string.Empty;
            public int SchemaVersion;
        }

        internal readonly Dictionary<string, SerializedEntry> Data = new();
    }
}

