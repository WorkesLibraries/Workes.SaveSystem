using System;
using System.Linq;
using System.Reflection;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class PublicApiShapeTests
{
    [Test]
    public void SaveSnapshot_Add_PreservesEntryValues()
    {
        var snapshot = new SaveSnapshot();
        var state = new object();

        snapshot.Add("player", schemaVersion: 2, state: state, loadPriority: -5);

        Assert.That(snapshot.Entries, Has.Count.EqualTo(1));
        Assert.That(snapshot.Entries[0].SaveKey, Is.EqualTo("player"));
        Assert.That(snapshot.Entries[0].SchemaVersion, Is.EqualTo(2));
        Assert.That(snapshot.Entries[0].State, Is.SameAs(state));
        Assert.That(snapshot.Entries[0].LoadPriority, Is.EqualTo(-5));
    }

    [Test]
    public void SaveSnapshot_Add_RejectsEmptySaveKey()
    {
        var snapshot = new SaveSnapshot();

        var ex = Assert.Throws<ArgumentException>(() => snapshot.Add(string.Empty, 1, new object()));

        Assert.That(ex!.ParamName, Is.EqualTo("key"));
    }

    [Test]
    public void SaveSnapshot_Add_RejectsSchemaVersionLessThanOne()
    {
        var snapshot = new SaveSnapshot();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Add("player", 0, new object()));

        Assert.That(ex!.ParamName, Is.EqualTo("schemaVersion"));
    }

    [Test]
    public void SaveSnapshot_Add_AllowsNullState()
    {
        var snapshot = new SaveSnapshot();

        snapshot.Add("player", 1, null);

        Assert.That(snapshot.Entries, Has.Count.EqualTo(1));
        Assert.That(snapshot.Entries[0].State, Is.Null);
    }

    [Test]
    public void SourceAssembly_UsesPackageRootNamespace()
    {
        var unexpectedTypes = typeof(SaveManager<>).Assembly
            .GetTypes()
            .Where(type => !type.FullName!.StartsWith("Microsoft.CodeAnalysis.", StringComparison.Ordinal))
            .Where(type => !type.FullName!.StartsWith("System.Runtime.CompilerServices.", StringComparison.Ordinal))
            .Where(type => type.Namespace != "Workes.SaveSystem")
            .Select(type => type.FullName)
            .ToArray();

        Assert.That(unexpectedTypes, Is.Empty);
    }

    [Test]
    public void MigrationCapableSerializers_ExposeNodeCreationThroughNodeFactoryOnly()
    {
        var jsonSerializer = new JsonSaveSerializer();

        Assert.That(jsonSerializer.Migration, Is.Not.Null);
        Assert.That(jsonSerializer, Is.Not.InstanceOf<ISaveDataNodeFactory>());
        Assert.That(jsonSerializer.Migration!.NodeFactory, Is.InstanceOf<ISaveDataNodeFactory>());
    }

    [Test]
    public void ContextualSerializerTypes_ArePublicOptionalApi()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Contain(nameof(SaveSerializerContext)));
        Assert.That(exportedTypeNames, Does.Contain(nameof(IContextualSaveSerializer)));
        Assert.That(exportedTypeNames, Does.Contain(nameof(IContextualSaveMigrationCapableSerializer)));
        Assert.That(
            typeof(ISaveDataNode).GetMethod(nameof(ISaveDataNode.ReplaceWith), new[] { typeof(ISaveDataNode) }),
            Is.Not.Null);

        var contextProperties = typeof(SaveSerializerContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.That(contextProperties, Does.Contain(nameof(SaveSerializerContext.SaveKey)));
        Assert.That(contextProperties, Does.Contain(nameof(SaveSerializerContext.SchemaVersion)));
        Assert.That(contextProperties, Does.Contain(nameof(SaveSerializerContext.StateType)));
        Assert.That(contextProperties, Does.Contain(nameof(SaveSerializerContext.Schematic)));
        Assert.That(contextProperties, Does.Contain(nameof(SaveSerializerContext.SerializerMetadata)));
    }

    [Test]
    public void TransformFactory_IsNotPublicApi()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Not.Contain("SaveSerializerTransforms"));
        Assert.That(exportedTypeNames, Does.Contain("TransformedSaveSerializer"));
        Assert.That(exportedTypeNames, Does.Contain("CompressedSaveSerializer"));
        Assert.That(exportedTypeNames, Does.Not.Contain("GZipPayloadTransform"));
    }

    [Test]
    public void BuiltInDataNodeImplementations_AreNotPublicApi()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Not.Contain("JsonSaveDataNode"));
        Assert.That(exportedTypeNames, Does.Not.Contain("JsonSaveDataNodeFactory"));
        Assert.That(exportedTypeNames, Does.Not.Contain("SaveDataNode"));
        Assert.That(exportedTypeNames, Does.Not.Contain("SaveDataNodeFactory"));
    }

    [Test]
    public void RemovedBinaryAndBase64JsonSerializerNames_AreNotPublicApi()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Not.Contain("BinarySaveSerializer"));
        Assert.That(exportedTypeNames, Does.Not.Contain("BinarySaveSchematic`1"));
        Assert.That(exportedTypeNames, Does.Not.Contain("Base64JsonSaveSerializer"));
        Assert.That(exportedTypeNames, Does.Not.Contain("Base64JsonSaveSchematic`1"));
    }

    [Test]
    public void SaveMetadata_IsPublicSerializerFacingContract()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Contain(nameof(SaveMetadata)));
        Assert.That(exportedTypeNames, Does.Contain(nameof(SaveApplicationMetadata)));
        Assert.That(exportedTypeNames, Does.Contain(nameof(SaveProviderManifestEntry)));
        Assert.That(exportedTypeNames, Does.Contain("ISaveMetadataProvider`1"));
        Assert.That(exportedTypeNames, Does.Contain(nameof(ISaveMetadataMigratable)));
        Assert.That(exportedTypeNames, Does.Contain(nameof(ISaveApplicationMetadataSerializer)));
        Assert.That(exportedTypeNames, Does.Contain("ISaveDefaultStateProvider`1"));

        var saveMetadataProperties = typeof(SaveMetadata)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.SaveId)));
        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.CreatedAtUtc)));
        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.LastWrittenAtUtc)));
        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.SerializerMetadata)));
        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.ProviderManifest)));
        Assert.That(saveMetadataProperties, Does.Contain(nameof(SaveMetadata.ApplicationMetadata)));

        var saveMetadataFields = typeof(SaveMetadata)
            .GetFields(BindingFlags.Public | BindingFlags.Instance);

        Assert.That(saveMetadataFields, Is.Empty);
        Assert.That(
            typeof(SaveApplicationMetadata).GetProperty(nameof(SaveApplicationMetadata.Data))!.PropertyType,
            Is.EqualTo(typeof(object)));
        Assert.That(
            typeof(SaveMetadata).GetMethod("CreateNewMetadata", BindingFlags.Public | BindingFlags.Static),
            Is.Null);
        Assert.That(
            typeof(SaveMetadata).GetMethod("PrepareForWrite", BindingFlags.Public | BindingFlags.Instance),
            Is.Null);

        var saveMetadataInfoProperties = typeof(SaveMetadataInfo)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.That(saveMetadataInfoProperties, Does.Not.Contain(nameof(SaveMetadata.SerializerMetadata)));
        Assert.That(saveMetadataInfoProperties, Does.Contain(nameof(SaveMetadataInfo.HasApplicationMetadata)));
        Assert.That(saveMetadataInfoProperties, Does.Contain(nameof(SaveMetadataInfo.ApplicationMetadataSchemaVersion)));
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.RegisterMetadataProvider)),
            Is.Not.Null);
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.TryRegisterMetadataProvider)),
            Is.Not.Null);
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.UnregisterMetadataProvider)),
            Is.Not.Null);
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.ReadApplicationMetadata)),
            Is.Not.Null);
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.ReadBackupApplicationMetadata)),
            Is.Not.Null);
    }

    [Test]
    public void SaveValidationResult_IsPublicValidationContract()
    {
        var exportedTypeNames = typeof(SaveManager<>).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.That(exportedTypeNames, Does.Contain(nameof(SaveValidationResult)));
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.ValidateSave)),
            Is.Not.Null);
        Assert.That(
            typeof(SaveManager<string>).GetMethod(nameof(SaveManager<string>.ValidateBackupSlot)),
            Is.Not.Null);

        var properties = typeof(SaveValidationResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.That(properties, Does.Contain(nameof(SaveValidationResult.IsValid)));
        Assert.That(properties, Does.Contain(nameof(SaveValidationResult.Status)));
        Assert.That(properties, Does.Contain(nameof(SaveValidationResult.Message)));
        Assert.That(properties, Does.Contain(nameof(SaveValidationResult.Exception)));
        Assert.That(properties, Does.Contain(nameof(SaveValidationResult.Metadata)));
    }
}
