using System;
using System.Collections.Generic;

namespace Workes.SaveSystem
{
    internal sealed class SerializedSnapshot
    {
        internal sealed class SerializedEntry
        {
            public byte[] Data = Array.Empty<byte>();
            public int SchemaVersion;
        }

        internal readonly Dictionary<string, SerializedEntry> Data = new();
    }
}

