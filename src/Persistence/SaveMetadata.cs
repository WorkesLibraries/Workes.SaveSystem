using System;

namespace Workes.SaveSystem
{
    [Serializable]
    internal sealed class SaveMetadata
    {
        public string SaveId = string.Empty;

        public static SaveMetadata CreateNewMetadata()
        {
            return new SaveMetadata
            {
                SaveId = Guid.NewGuid().ToString()
            };
        }
    }
}
