using System;
using System.Collections.Generic;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class DataNodeContractTests
{
    [Test]
    public void JsonNodeFactory_CreateNullCreatesNullNode()
    {
        var factory = new JsonSaveDataNodeFactory();

        var node = factory.CreateNull();

        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(node.IsObject(), Is.False);
        Assert.That(node.IsArray(), Is.False);
        Assert.That(node.Count, Is.EqualTo(0));
    }

    [Test]
    public void JsonObjectNode_SupportsObjectOperations()
    {
        var factory = new JsonSaveDataNodeFactory();
        var node = factory.CreateObject();

        node.Set("name", factory.CreateString("Rook"));
        node.Set("level", factory.CreateInt(4));
        node.Set("deletedAt", factory.CreateNull());

        Assert.That(node.Has("name"), Is.True);
        Assert.That(node.Get("name").AsString(), Is.EqualTo("Rook"));
        Assert.That(node.Get("level").AsInt(), Is.EqualTo(4));
        Assert.That(node.Get("deletedAt").NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(node.Remove("level"), Is.True);
        Assert.That(node.Has("level"), Is.False);
    }

    [Test]
    public void JsonArrayNode_SupportsArrayOperations()
    {
        var factory = new JsonSaveDataNodeFactory();
        var node = factory.CreateArray();

        node.Add(factory.CreateString("first"));
        node.InsertAt(1, factory.CreateString("third"));
        node.SetAt(1, factory.CreateString("second"));

        Assert.That(node.Count, Is.EqualTo(2));
        Assert.That(node.GetAt(0).AsString(), Is.EqualTo("first"));
        Assert.That(node.GetAt(1).AsString(), Is.EqualTo("second"));
        Assert.That(node.RemoveAt(0), Is.True);
        Assert.That(node.GetAt(0).AsString(), Is.EqualTo("second"));
    }

    [Test]
    public void JsonDataNode_ThrowsClearlyForWrongShapeOperations()
    {
        var factory = new JsonSaveDataNodeFactory();
        var obj = factory.CreateObject();
        var array = factory.CreateArray();
        var primitive = factory.CreateInt(1);

        Assert.Throws<InvalidOperationException>(() => obj.GetAt(0));
        Assert.Throws<InvalidOperationException>(() => array.Get("name"));
        Assert.Throws<InvalidOperationException>(() => primitive.Add(factory.CreateInt(2)));
        Assert.Throws<InvalidOperationException>(() => obj.Get("missing"));
    }

    [Test]
    public void JsonDataNode_RejectsNodesFromOtherImplementations()
    {
        var factory = new JsonSaveDataNodeFactory();
        var node = factory.CreateObject();

        var ex = Assert.Throws<InvalidOperationException>(() => node.Set("foreign", new ForeignNode()));

        Assert.That(ex!.Message, Does.Contain("JSON data nodes"));
    }

    private sealed class ForeignNode : ISaveDataNode
    {
        public SaveDataNodeType NodeType => SaveDataNodeType.String;
        public bool IsObject() => false;
        public bool IsArray() => false;
        public int Count => 0;
        public IEnumerable<string> Keys => Array.Empty<string>();
        public ISaveDataNode GetAt(int index) => throw new NotSupportedException();
        public void SetAt(int index, ISaveDataNode value) => throw new NotSupportedException();
        public bool InsertAt(int index, ISaveDataNode value) => throw new NotSupportedException();
        public bool RemoveAt(int index) => false;
        public void Add(ISaveDataNode value) => throw new NotSupportedException();
        public bool Has(string key) => false;
        public ISaveDataNode Get(string key) => throw new NotSupportedException();
        public void Set(string key, ISaveDataNode value) => throw new NotSupportedException();
        public bool Remove(string key) => false;
        public int AsInt() => throw new NotSupportedException();
        public void SetInt(int value) => throw new NotSupportedException();
        public float AsFloat() => throw new NotSupportedException();
        public void SetFloat(float value) => throw new NotSupportedException();
        public string AsString() => "foreign";
        public void SetString(string value) => throw new NotSupportedException();
        public bool AsBool() => throw new NotSupportedException();
        public void SetBool(bool value) => throw new NotSupportedException();
    }
}
