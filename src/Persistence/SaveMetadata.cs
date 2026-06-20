using System;

namespace Workes.SaveSystem
{
    [Serializable]
    internal sealed class SaveMetadata
    {
        public string SaveId = string.Empty;
        public DateTimeOffset CreatedAtUtc;
        public DateTimeOffset LastWrittenAtUtc;

        public static SaveMetadata CreateNewMetadata(DateTimeOffset? timestampUtc = null)
        {
            var timestamp = timestampUtc ?? DateTimeOffset.UtcNow;
            return new SaveMetadata
            {
                SaveId = Guid.NewGuid().ToString(),
                CreatedAtUtc = timestamp,
                LastWrittenAtUtc = timestamp
            };
        }

        public void PrepareForWrite(DateTimeOffset timestampUtc)
        {
            if (string.IsNullOrEmpty(SaveId))
                SaveId = Guid.NewGuid().ToString();

            if (CreatedAtUtc == default)
                CreatedAtUtc = timestampUtc;

            LastWrittenAtUtc = timestampUtc;
        }
    }
}
