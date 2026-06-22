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

        public ISaveDataNode CreateLong(long value)
        {
            return SaveDataNode.CreateLong(value, Owner);
        }

        public ISaveDataNode CreateFloat(float value)
        {
            return SaveDataNode.CreateFloat(value, Owner);
        }

        public ISaveDataNode CreateDouble(double value)
        {
            return SaveDataNode.CreateDouble(value, Owner);
        }

        public ISaveDataNode CreateDecimal(decimal value)
        {
            return SaveDataNode.CreateDecimal(value, Owner);
        }

        public ISaveDataNode CreateString(string value)
        {
            return SaveDataNode.CreateString(value, Owner);
        }

        public ISaveDataNode CreateBool(bool value)
        {
            return SaveDataNode.CreateBool(value, Owner);
        }

        public ISaveDataNode CreateBytes(byte[] value)
        {
            return SaveDataNode.CreateBytes(value, Owner);
        }

        public ISaveDataNode CreateDateTime(System.DateTime value)
        {
            return SaveDataNode.CreateDateTime(value, Owner);
        }

        public ISaveDataNode CreateNull()
        {
            return SaveDataNode.CreateNull(Owner);
        }
    }
}
