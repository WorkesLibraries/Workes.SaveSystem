using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.SaveSystem
{
    internal sealed class JsonSaveDataNode : ISaveDataNode
    {
        internal readonly JToken _token;
        private readonly object _owner;

        public JsonSaveDataNode(JToken token, object owner)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public SaveDataNodeType NodeType
        {
            get
            {
                switch (_token.Type)
                {
                    case JTokenType.Object:
                        return SaveDataNodeType.Object;
                    case JTokenType.Array:
                        return SaveDataNodeType.Array;
                    case JTokenType.Integer:
                        return SaveDataNodeType.Int;
                    case JTokenType.Float:
                        return SaveDataNodeType.Float;
                    case JTokenType.String:
                        return SaveDataNodeType.String;
                    case JTokenType.Boolean:
                        return SaveDataNodeType.Bool;
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                        return SaveDataNodeType.Null;
                    default:
                        return SaveDataNodeType.String;
                }
            }
        }

        public bool IsObject() => _token is JObject;
        public bool IsArray() => _token is JArray;

        public int Count
        {
            get
            {
                if (_token is JArray array)
                    return array.Count;
                if (_token is JObject obj)
                    return obj.Count;
                return 0;
            }
        }

        public ISaveDataNode GetAt(int index)
        {
            if (_token is JArray array)
                return new JsonSaveDataNode(array[index], _owner);
            throw new InvalidOperationException("Node is not an array");
        }

        public void SetAt(int index, ISaveDataNode value)
        {
            if (_token is JArray array)
            {
                array[index] = RequireJsonNode(value, _owner)._token;
                return;
            }
            throw new InvalidOperationException("Node is not an array");
        }

        public bool InsertAt(int index, ISaveDataNode value)
        {
            if (_token is JArray array)
            {
                if (index < 0 || index > array.Count)
                    return false;
                array.Insert(index, RequireJsonNode(value, _owner)._token);
                return true;
            }
            return false;
        }

        public bool RemoveAt(int index)
        {
            if (_token is JArray array)
            {
                if (index < 0 || index >= array.Count)
                    return false;
                array.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void Add(ISaveDataNode value)
        {
            if (_token is JArray array)
            {
                array.Add(RequireJsonNode(value, _owner)._token);
                return;
            }
            throw new InvalidOperationException("Node is not an array");
        }

        public bool Has(string key)
        {
            if (_token is JObject obj)
                return obj[key] != null;

            throw new InvalidOperationException("Node is not an object");
        }

        public ISaveDataNode Get(string key)
        {
            if (!(_token is JObject obj))
                throw new InvalidOperationException("Node is not an object");

            var child = obj[key];
            if (child == null)
                throw new InvalidOperationException($"Node does not contain key '{key}'.");

            return new JsonSaveDataNode(child, _owner);
        }

        public void Set(string key, ISaveDataNode value)
        {
            if (_token is JObject obj)
            {
                obj[key] = RequireJsonNode(value, _owner)._token;
                return;
            }

            throw new InvalidOperationException("Node is not an object");
        }

        public bool Remove(string key)
        {
            if (_token is JObject obj)
            {
                return obj.Remove(key);
            }
            return false;
        }

        public int AsInt() => _token.Value<int>();
        public void SetInt(int value) => _token.Replace(value);

        public float AsFloat() => _token.Value<float>();
        public void SetFloat(float value) => _token.Replace(value);

        public string AsString() => _token.Value<string>() ?? throw new InvalidOperationException("Node value is not a string");
        public void SetString(string value) => _token.Replace(value);

        public bool AsBool() => _token.Value<bool>();
        public void SetBool(bool value) => _token.Replace(value);

        public IEnumerable<string> Keys =>
            _token is JObject obj ? obj.Properties().Select(p => p.Name) : Enumerable.Empty<string>();

        internal static JsonSaveDataNode RequireJsonNode(ISaveDataNode value, object owner)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value is JsonSaveDataNode jsonNode && ReferenceEquals(jsonNode._owner, owner))
                return jsonNode;

            throw new InvalidOperationException("JSON data nodes can only be combined with nodes created by the same node factory.");
        }
    }
}
