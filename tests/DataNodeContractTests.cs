using System;
using System.Collections.Generic;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class DataNodeContractTests
{
    [Test]
    public void SaveDataNodeFactory_CreateNullCreatesNullNode()
    {
        var factory = CreateFactory();

        var node = factory.CreateNull();

        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(node.IsObject(), Is.False);
        Assert.That(node.IsArray(), Is.False);
        Assert.That(node.IsNull(), Is.True);
        Assert.That(node.Count, Is.EqualTo(0));
    }

    [Test]
    public void SaveDataNode_IsNullReturnsFalseForNonNullNodes()
    {
        var factory = CreateFactory();

        Assert.That(factory.CreateObject().IsNull(), Is.False);
        Assert.That(factory.CreateArray().IsNull(), Is.False);
        Assert.That(factory.CreateInt(1).IsNull(), Is.False);
        Assert.That(factory.CreateFloat(1).IsNull(), Is.False);
        Assert.That(factory.CreateString("Rook").IsNull(), Is.False);
        Assert.That(factory.CreateBool(true).IsNull(), Is.False);
    }

    [Test]
    public void SaveDataNode_SetNullChangesExistingNodeToNull()
    {
        var factory = CreateFactory();
        var obj = factory.CreateObject();
        obj.Set("name", factory.CreateString("Rook"));
        var array = factory.CreateArray();
        array.Add(factory.CreateInt(1));
        var primitive = factory.CreateString("Rook");

        obj.SetNull();
        array.SetNull();
        primitive.SetNull();

        Assert.That(obj.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(obj.IsNull(), Is.True);
        Assert.That(obj.Count, Is.EqualTo(0));
        Assert.That(array.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(array.IsNull(), Is.True);
        Assert.That(array.Count, Is.EqualTo(0));
        Assert.That(primitive.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(primitive.IsNull(), Is.True);
    }

    [Test]
    public void SaveDataObjectNode_SupportsObjectOperations()
    {
        var factory = CreateFactory();
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
    public void SaveDataArrayNode_SupportsArrayOperations()
    {
        var factory = CreateFactory();
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
    public void SaveDataNode_ThrowsClearlyForWrongShapeOperations()
    {
        var factory = CreateFactory();
        var obj = factory.CreateObject();
        var array = factory.CreateArray();
        var primitive = factory.CreateInt(1);

        Assert.Throws<InvalidOperationException>(() => obj.GetAt(0));
        Assert.Throws<InvalidOperationException>(() => array.Get("name"));
        Assert.Throws<InvalidOperationException>(() => primitive.Add(factory.CreateInt(2)));
        Assert.Throws<InvalidOperationException>(() => obj.Get("missing"));
    }

    [Test]
    public void SaveDataNode_RejectsNodesFromOtherImplementations()
    {
        var factory = CreateFactory();
        var node = factory.CreateObject();

        var ex = Assert.Throws<InvalidOperationException>(() => node.Set("foreign", new ForeignNode()));

        Assert.That(ex!.Message, Does.Contain("Save data nodes"));
    }

    [Test]
    public void SaveDataNode_RejectsNodesFromDifferentFactories()
    {
        var factory = CreateFactory();
        var otherFactory = CreateFactory();
        var node = factory.CreateObject();

        var ex = Assert.Throws<InvalidOperationException>(() => node.Set("foreign", otherFactory.CreateString("Rook")));

        Assert.That(ex!.Message, Does.Contain("same node factory"));
    }

    [Test]
    public void MigrationCapableSerializers_RejectNodesFromOtherSerializerFactories()
    {
        var jsonSerializer = new JsonSaveSerializer();
        var otherJsonSerializer = new JsonSaveSerializer();
        var jsonNode = jsonSerializer.NodeFactory.CreateObject();
        var otherJsonNode = otherJsonSerializer.NodeFactory.CreateObject();

        var jsonEx = Assert.Throws<InvalidOperationException>(() => jsonSerializer.SerializeFromNode(otherJsonNode));
        var otherJsonEx = Assert.Throws<InvalidOperationException>(() => otherJsonSerializer.SerializeFromNode(jsonNode));

        Assert.That(jsonEx!.Message, Does.Contain("same node factory"));
        Assert.That(otherJsonEx!.Message, Does.Contain("same node factory"));
    }

    private static ISaveDataNodeFactory CreateFactory()
    {
        return new JsonSaveSerializer().NodeFactory;
    }

    private sealed class ForeignNode : ISaveDataNode
    {
        public SaveDataNodeType NodeType => SaveDataNodeType.String;
        public bool IsObject() => false;
        public bool IsArray() => false;
        public bool IsNull() => false;
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
        public void SetNull() => throw new NotSupportedException();
    }
}
