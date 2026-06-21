namespace Workes.SaveSystem
{
    internal sealed class SaveDataNodeFactory : ISaveDataNodeFactory
    {
        internal object Owner { get; } = new object();

        public ISaveDataNode CreateObject()
        {
            return SaveDataNode.CreateObject(Owner);
        }

        public ISaveDataNode CreateArray()
        {
            return SaveDataNode.CreateArray(Owner);
        }

        public ISaveDataNode CreateInt(int value)
        {
            return SaveDataNode.CreateInt(value, Owner);
        }

        public ISaveDataNode CreateFloat(float value)
        {
            return SaveDataNode.CreateFloat(value, Owner);
        }

        public ISaveDataNode CreateString(string value)
        {
            return SaveDataNode.CreateString(value, Owner);
        }

        public ISaveDataNode CreateBool(bool value)
        {
            return SaveDataNode.CreateBool(value, Owner);
        }

        public ISaveDataNode CreateNull()
        {
            return SaveDataNode.CreateNull(Owner);
        }
    }
}
