using System;

namespace Workes.SaveSystem
{
    [Serializable]
    internal sealed class VersionedPayload<T>
    {
        public int SchemaVersion;
        public T? Data;
    }
}
