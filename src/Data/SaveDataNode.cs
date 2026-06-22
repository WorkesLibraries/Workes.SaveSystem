using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Workes.SaveSystem
{
    internal sealed class SaveDataNode : ISaveDataNode
    {
        private readonly object _owner;
        private SaveDataNodeType _nodeType;
        private List<KeyValuePair<string, SaveDataNode>> _objectChildren = new List<KeyValuePair<string, SaveDataNode>>();
        private List<SaveDataNode> _arrayChildren = new List<SaveDataNode>();
        private object _value = new object();

        private SaveDataNode(SaveDataNodeType nodeType, object owner)
        {
            _nodeType = nodeType;
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public SaveDataNodeType NodeType => _nodeType;

        public bool IsObject() => _nodeType == SaveDataNodeType.Object;

        public bool IsArray() => _nodeType == SaveDataNodeType.Array;

        public bool IsNull() => _nodeType == SaveDataNodeType.Null;

        public int Count
        {
            get
            {
                if (IsObject())
                    return _objectChildren.Count;
                if (IsArray())
                    return _arrayChildren.Count;
                return 0;
            }
        }

        public IEnumerable<string> Keys =>
            IsObject() ? _objectChildren.Select(child => child.Key) : Enumerable.Empty<string>();

        internal static SaveDataNode CreateObject(object owner)
        {
            return new SaveDataNode(SaveDataNodeType.Object, owner)
            {
                _objectChildren = new List<KeyValuePair<string, SaveDataNode>>()
            };
        }

        internal static SaveDataNode CreateArray(object owner)
        {
            return new SaveDataNode(SaveDataNodeType.Array, owner)
            {
                _arrayChildren = new List<SaveDataNode>()
            };
        }

        internal static SaveDataNode CreateInt(int value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Int, value, owner);
        }

        internal static SaveDataNode CreateLong(long value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Long, value, owner);
        }

        internal static SaveDataNode CreateFloat(float value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Float, value, owner);
        }

        internal static SaveDataNode CreateDouble(double value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Double, value, owner);
        }

        internal static SaveDataNode CreateDecimal(decimal value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Decimal, value, owner);
        }

        internal static SaveDataNode CreateString(string value, object owner)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return CreatePrimitive(SaveDataNodeType.String, value, owner);
        }

        internal static SaveDataNode CreateBool(bool value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.Bool, value, owner);
        }

        internal static SaveDataNode CreateBytes(byte[] value, object owner)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return CreatePrimitive(SaveDataNodeType.Bytes, (byte[])value.Clone(), owner);
        }

        internal static SaveDataNode CreateDateTime(DateTime value, object owner)
        {
            return CreatePrimitive(SaveDataNodeType.DateTime, value, owner);
        }

        internal static SaveDataNode CreateNull(object owner)
        {
            return new SaveDataNode(SaveDataNodeType.Null, owner);
        }

        public ISaveDataNode GetAt(int index)
        {
            if (!IsArray())
                throw new InvalidOperationException("Node is not an array");

            return _arrayChildren[index];
        }

        public void SetAt(int index, ISaveDataNode value)
        {
            if (!IsArray())
                throw new InvalidOperationException("Node is not an array");

            _arrayChildren[index] = RequireSaveDataNode(value, _owner);
        }

        public bool InsertAt(int index, ISaveDataNode value)
        {
            if (!IsArray())
                return false;

            if (index < 0 || index > _arrayChildren.Count)
                return false;

            _arrayChildren.Insert(index, RequireSaveDataNode(value, _owner));
            return true;
        }

        public bool RemoveAt(int index)
        {
            if (!IsArray())
                return false;

            if (index < 0 || index >= _arrayChildren.Count)
                return false;

            _arrayChildren.RemoveAt(index);
            return true;
        }

        public void Add(ISaveDataNode value)
        {
            if (!IsArray())
                throw new InvalidOperationException("Node is not an array");

            _arrayChildren.Add(RequireSaveDataNode(value, _owner));
        }

        public bool Has(string key)
        {
            if (!IsObject())
                throw new InvalidOperationException("Node is not an object");

            return FindObjectIndex(key) >= 0;
        }

        public ISaveDataNode Get(string key)
        {
            if (!IsObject())
                throw new InvalidOperationException("Node is not an object");

            var index = FindObjectIndex(key);
            if (index < 0)
                throw new InvalidOperationException($"Node does not contain key '{key}'.");

            return _objectChildren[index].Value;
        }

        public void Set(string key, ISaveDataNode value)
        {
            if (!IsObject())
                throw new InvalidOperationException("Node is not an object");

            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var child = RequireSaveDataNode(value, _owner);
            var index = FindObjectIndex(key);
            var entry = new KeyValuePair<string, SaveDataNode>(key, child);

            if (index >= 0)
            {
                _objectChildren[index] = entry;
                return;
            }

            _objectChildren.Add(entry);
        }

        public bool Remove(string key)
        {
            if (!IsObject())
                return false;

            var index = FindObjectIndex(key);
            if (index < 0)
                return false;

            _objectChildren.RemoveAt(index);
            return true;
        }

        public int AsInt()
        {
            if (_nodeType == SaveDataNodeType.Int)
                return (int)_value;

            if (_nodeType == SaveDataNodeType.Long)
            {
                var value = (long)_value;
                if (value >= int.MinValue && value <= int.MaxValue)
                    return (int)value;
            }

            throw CreateWrongTypeException(SaveDataNodeType.Int);
        }

        public void SetInt(int value)
        {
            SetPrimitive(SaveDataNodeType.Int, value);
        }

        public long AsLong()
        {
            if (_nodeType == SaveDataNodeType.Long)
                return (long)_value;

            if (_nodeType == SaveDataNodeType.Int)
                return (int)_value;

            throw CreateWrongTypeException(SaveDataNodeType.Long);
        }

        public void SetLong(long value)
        {
            SetPrimitive(SaveDataNodeType.Long, value);
        }

        public float AsFloat()
        {
            if (_nodeType == SaveDataNodeType.Float)
                return (float)_value;

            if (_nodeType == SaveDataNodeType.Double)
            {
                var value = (double)_value;
                if (value >= -float.MaxValue && value <= float.MaxValue)
                    return (float)value;
            }

            throw CreateWrongTypeException(SaveDataNodeType.Float);
        }

        public void SetFloat(float value)
        {
            SetPrimitive(SaveDataNodeType.Float, value);
        }

        public double AsDouble()
        {
            if (_nodeType == SaveDataNodeType.Double)
                return (double)_value;

            if (_nodeType == SaveDataNodeType.Float)
                return (float)_value;

            throw CreateWrongTypeException(SaveDataNodeType.Double);
        }

        public void SetDouble(double value)
        {
            SetPrimitive(SaveDataNodeType.Double, value);
        }

        public decimal AsDecimal()
        {
            if (_nodeType == SaveDataNodeType.Decimal)
                return (decimal)_value;

            if (_nodeType == SaveDataNodeType.String)
            {
                var text = (string)_value;
                try
                {
                    return decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("String node does not contain a valid invariant decimal value.", ex);
                }
            }

            throw CreateWrongTypeException(SaveDataNodeType.Decimal);
        }

        public void SetDecimal(decimal value)
        {
            SetPrimitive(SaveDataNodeType.Decimal, value);
        }

        public string AsString()
        {
            EnsureNodeType(SaveDataNodeType.String);
            return (string)_value;
        }

        public void SetString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            SetPrimitive(SaveDataNodeType.String, value);
        }

        public bool AsBool()
        {
            EnsureNodeType(SaveDataNodeType.Bool);
            return (bool)_value;
        }

        public void SetBool(bool value)
        {
            SetPrimitive(SaveDataNodeType.Bool, value);
        }

        public byte[] AsBytes()
        {
            if (_nodeType == SaveDataNodeType.Bytes)
                return (byte[])((byte[])_value).Clone();

            if (_nodeType == SaveDataNodeType.String)
            {
                var text = (string)_value;
                try
                {
                    return Convert.FromBase64String(text);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("String node does not contain a valid Base64 byte-array value.", ex);
                }
            }

            throw CreateWrongTypeException(SaveDataNodeType.Bytes);
        }

        public void SetBytes(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            SetPrimitive(SaveDataNodeType.Bytes, (byte[])value.Clone());
        }

        public DateTime AsDateTime()
        {
            if (_nodeType == SaveDataNodeType.DateTime)
                return (DateTime)_value;

            if (_nodeType == SaveDataNodeType.String)
            {
                var text = (string)_value;
                try
                {
                    return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("String node does not contain a valid round-trip date/time value.", ex);
                }
            }

            throw CreateWrongTypeException(SaveDataNodeType.DateTime);
        }

        public void SetDateTime(DateTime value)
        {
            SetPrimitive(SaveDataNodeType.DateTime, value);
        }

        public void SetNull()
        {
            _nodeType = SaveDataNodeType.Null;
            _objectChildren.Clear();
            _arrayChildren.Clear();
            _value = new object();
        }

        internal static SaveDataNode RequireSaveDataNode(ISaveDataNode value, object owner)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value is SaveDataNode node && ReferenceEquals(node._owner, owner))
                return node;

            throw new InvalidOperationException("Save data nodes can only be combined with nodes created by the same node factory.");
        }

        private static SaveDataNode CreatePrimitive(SaveDataNodeType nodeType, object value, object owner)
        {
            return new SaveDataNode(nodeType, owner)
            {
                _value = value
            };
        }

        private int FindObjectIndex(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            for (var i = 0; i < _objectChildren.Count; i++)
            {
                if (StringComparer.Ordinal.Equals(_objectChildren[i].Key, key))
                    return i;
            }

            return -1;
        }

        private void EnsureNodeType(SaveDataNodeType nodeType)
        {
            if (_nodeType != nodeType)
                throw CreateWrongTypeException(nodeType);
        }

        private InvalidOperationException CreateWrongTypeException(SaveDataNodeType expectedType)
        {
            return new InvalidOperationException($"Node is not a {expectedType} value");
        }

        private void SetPrimitive(SaveDataNodeType nodeType, object value)
        {
            _nodeType = nodeType;
            _objectChildren.Clear();
            _arrayChildren.Clear();
            _value = value;
        }
    }
}
