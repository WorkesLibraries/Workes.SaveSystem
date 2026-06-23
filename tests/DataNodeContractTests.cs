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
        Assert.That(factory.CreateLong(1L).IsNull(), Is.False);
        Assert.That(factory.CreateFloat(1).IsNull(), Is.False);
        Assert.That(factory.CreateDouble(1).IsNull(), Is.False);
        Assert.That(factory.CreateDecimal(1m).IsNull(), Is.False);
        Assert.That(factory.CreateString("Rook").IsNull(), Is.False);
        Assert.That(factory.CreateBool(true).IsNull(), Is.False);
        Assert.That(factory.CreateBytes(new byte[] { 1 }).IsNull(), Is.False);
        Assert.That(factory.CreateDateTime(DateTime.UtcNow).IsNull(), Is.False);
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
    public void SaveDataNode_ReplaceWith_ReplacesNodeShapeAndValue()
    {
        var factory = CreateFactory();
        var root = factory.CreateObject();
        root.Set("name", factory.CreateString("Rook"));
        var array = factory.CreateArray();
        array.Add(factory.CreateInt(1));

        root.ReplaceWith(array);

        Assert.That(root.NodeType, Is.EqualTo(SaveDataNodeType.Array));
        Assert.That(root.Count, Is.EqualTo(1));
        Assert.That(root.GetAt(0).AsInt(), Is.EqualTo(1));

        var obj = factory.CreateObject();
        obj.Set("level", factory.CreateInt(4));
        root.ReplaceWith(obj);

        Assert.That(root.NodeType, Is.EqualTo(SaveDataNodeType.Object));
        Assert.That(root.Get("level").AsInt(), Is.EqualTo(4));

        root.ReplaceWith(factory.CreateString("done"));

        Assert.That(root.NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(root.AsString(), Is.EqualTo("done"));

        root.ReplaceWith(factory.CreateNull());

        Assert.That(root.NodeType, Is.EqualTo(SaveDataNodeType.Null));
        Assert.That(root.IsNull(), Is.True);
    }

    [Test]
    public void SaveDataNode_ReplaceWith_ClonesReplacementTree()
    {
        var factory = CreateFactory();
        var root = factory.CreateObject();
        var replacement = factory.CreateObject();
        var bytes = new byte[] { 1, 2, 3 };
        var child = factory.CreateArray();
        child.Add(factory.CreateBytes(bytes));
        replacement.Set("items", child);

        root.ReplaceWith(replacement);
        bytes[0] = 9;
        replacement.Get("items").GetAt(0).SetBytes(new byte[] { 4, 5, 6 });

        Assert.That(root.Get("items").GetAt(0).AsBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));
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
    public void SaveDataNode_PrimitiveValuesRoundTrip()
    {
        var factory = CreateFactory();
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3, 4 };
        var node = factory.CreateObject();

        node.Set("long", factory.CreateLong(9_000_000_000L));
        node.Set("double", factory.CreateDouble(123.456789d));
        node.Set("decimal", factory.CreateDecimal(1234567890.123456789m));
        node.Set("bytes", factory.CreateBytes(bytes));
        node.Set("dateTime", factory.CreateDateTime(dateTime));

        Assert.That(node.Get("long").NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(node.Get("long").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(node.Get("double").NodeType, Is.EqualTo(SaveDataNodeType.Double));
        Assert.That(node.Get("double").AsDouble(), Is.EqualTo(123.456789d));
        Assert.That(node.Get("decimal").NodeType, Is.EqualTo(SaveDataNodeType.Decimal));
        Assert.That(node.Get("decimal").AsDecimal(), Is.EqualTo(1234567890.123456789m));
        Assert.That(node.Get("bytes").NodeType, Is.EqualTo(SaveDataNodeType.Bytes));
        Assert.That(node.Get("bytes").AsBytes(), Is.EqualTo(bytes));
        Assert.That(node.Get("dateTime").NodeType, Is.EqualTo(SaveDataNodeType.DateTime));
        Assert.That(node.Get("dateTime").AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void SaveDataNode_SetPrimitiveValuesReplacesNodeTypes()
    {
        var node = CreateFactory().CreateString("start");
        var bytes = new byte[] { 9, 8, 7 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);

        node.SetLong(9_000_000_000L);
        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(node.AsLong(), Is.EqualTo(9_000_000_000L));

        node.SetDouble(12.5d);
        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Double));
        Assert.That(node.AsDouble(), Is.EqualTo(12.5d));

        node.SetDecimal(12.345m);
        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Decimal));
        Assert.That(node.AsDecimal(), Is.EqualTo(12.345m));

        node.SetBytes(bytes);
        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.Bytes));
        Assert.That(node.AsBytes(), Is.EqualTo(bytes));

        node.SetDateTime(dateTime);
        Assert.That(node.NodeType, Is.EqualTo(SaveDataNodeType.DateTime));
        Assert.That(node.AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void SaveDataNode_StringConversionsSupportDocumentedJsonConventions()
    {
        var factory = CreateFactory();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);

        var bytesNode = factory.CreateString(Convert.ToBase64String(bytes));
        var decimalNode = factory.CreateString("1234567890.123456789");
        var dateTimeNode = factory.CreateString(dateTime.ToString("O"));

        Assert.That(bytesNode.AsBytes(), Is.EqualTo(bytes));
        Assert.That(decimalNode.AsDecimal(), Is.EqualTo(1234567890.123456789m));
        Assert.That(dateTimeNode.AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void SaveDataNode_StringConversionsRejectInvalidValues()
    {
        var factory = CreateFactory();

        Assert.Throws<InvalidOperationException>(() => factory.CreateString("not base64").AsBytes());
        Assert.Throws<InvalidOperationException>(() => factory.CreateString("not decimal").AsDecimal());
        Assert.Throws<InvalidOperationException>(() => factory.CreateString("not date").AsDateTime());
    }

    [Test]
    public void SaveDataNode_CompatibleNumericReadsConvertSafely()
    {
        var factory = CreateFactory();

        Assert.That(factory.CreateInt(3).AsLong(), Is.EqualTo(3L));
        Assert.That(factory.CreateLong(3L).AsInt(), Is.EqualTo(3));
        Assert.That(factory.CreateFloat(3.5f).AsDouble(), Is.EqualTo(3.5d).Within(0.0001d));
        Assert.That(factory.CreateDouble(3.5d).AsFloat(), Is.EqualTo(3.5f).Within(0.0001f));
        Assert.Throws<InvalidOperationException>(() => factory.CreateLong(9_000_000_000L).AsInt());
        Assert.Throws<InvalidOperationException>(() => factory.CreateDouble(double.MaxValue).AsFloat());
    }

    [Test]
    public void SaveDataNode_BytesAreCopiedOnSetAndRead()
    {
        var factory = CreateFactory();
        var bytes = new byte[] { 1, 2, 3 };
        var node = factory.CreateBytes(bytes);
        bytes[0] = 9;

        var read = node.AsBytes();
        read[1] = 9;

        Assert.That(node.AsBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));
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
        var replaceEx = Assert.Throws<InvalidOperationException>(() => node.ReplaceWith(new ForeignNode()));

        Assert.That(ex!.Message, Does.Contain("Save data nodes"));
        Assert.That(replaceEx!.Message, Does.Contain("Save data nodes"));
    }

    [Test]
    public void SaveDataNode_RejectsNodesFromDifferentFactories()
    {
        var factory = CreateFactory();
        var otherFactory = CreateFactory();
        var node = factory.CreateObject();

        var ex = Assert.Throws<InvalidOperationException>(() => node.Set("foreign", otherFactory.CreateString("Rook")));
        var replaceEx = Assert.Throws<InvalidOperationException>(() => node.ReplaceWith(otherFactory.CreateString("Rook")));

        Assert.That(ex!.Message, Does.Contain("same node factory"));
        Assert.That(replaceEx!.Message, Does.Contain("same node factory"));
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
        public long AsLong() => throw new NotSupportedException();
        public void SetLong(long value) => throw new NotSupportedException();
        public float AsFloat() => throw new NotSupportedException();
        public void SetFloat(float value) => throw new NotSupportedException();
        public double AsDouble() => throw new NotSupportedException();
        public void SetDouble(double value) => throw new NotSupportedException();
        public decimal AsDecimal() => throw new NotSupportedException();
        public void SetDecimal(decimal value) => throw new NotSupportedException();
        public string AsString() => "foreign";
        public void SetString(string value) => throw new NotSupportedException();
        public bool AsBool() => throw new NotSupportedException();
        public void SetBool(bool value) => throw new NotSupportedException();
        public byte[] AsBytes() => throw new NotSupportedException();
        public void SetBytes(byte[] value) => throw new NotSupportedException();
        public DateTime AsDateTime() => throw new NotSupportedException();
        public void SetDateTime(DateTime value) => throw new NotSupportedException();
        public void SetNull() => throw new NotSupportedException();
        public void ReplaceWith(ISaveDataNode value) => throw new NotSupportedException();
    }
}
