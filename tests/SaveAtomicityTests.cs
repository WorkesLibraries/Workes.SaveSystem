using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveAtomicityTests
{
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Workes.SaveSystem.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void SaveToDisk_WhenProviderCaptureThrows_LeavesExistingSaveLoadableAndRemovesTempFolder()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        provider.Current = new TestState { Value = 2 };
        provider.ThrowOnCapture = true;

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        provider.ThrowOnCapture = false;
        provider.Current = new TestState { Value = 99 };
        var loaded = manager.LoadFromDisk("slot");

        Assert.That(ex!.Message, Does.Contain("capture failed"));
        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenFileNameResolverReturnsInvalidName_LeavesExistingSaveLoadableAndRemovesTempFolder()
    {
        var goodManager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        goodManager.RegisterProvider(provider);
        SaveValue(goodManager, provider, "slot", 1);

        var badManager = CreateManager(fileNameResolver: _ => "bad/name");
        badManager.RegisterProvider(provider);
        provider.Current = new TestState { Value = 2 };

        var ex = Assert.Throws<InvalidOperationException>(() => badManager.ValidateRegistrations());

        var loadManager = CreateManager();
        loadManager.RegisterProvider(provider);
        loadManager.ValidateRegistrations();
        provider.Current = new TestState { Value = 99 };
        var loaded = loadManager.LoadFromDisk("slot");

        Assert.That(ex!.Message, Does.Contain("invalid characters"));
        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void SaveToDisk_WhenTempValidationDeserializesWrongStateType_RemovesTempAndDoesNotPromoteSave()
    {
        var manager = CreateManager(serializer: new WrongStateTypeSerializer());
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("deserialize correctly"));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void ForceSaveToDisk_WhenProviderCaptureThrows_LeavesExistingSaveLoadableAndRemovesTempFolder()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        provider.Current = new TestState { Value = 2 };
        provider.ThrowOnCapture = true;

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ForceSaveToDisk("slot"));

        provider.ThrowOnCapture = false;
        provider.Current = new TestState { Value = 99 };
        var loaded = manager.LoadFromDisk("slot");

        Assert.That(ex!.Message, Does.Contain("capture failed"));
        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void ForceSaveToDisk_RequiresValidatedRegistrations()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ForceSaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    private SaveManager<string> CreateManager(
        Func<SaveFileContext, string>? fileNameResolver = null,
        ISaveSerializer? serializer = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer ?? new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: fileNameResolver ?? SaveSystemOptions<string>.DefaultFileNameResolver));
    }

    private static void SaveValue(
        SaveManager<string> manager,
        TestProvider provider,
        string slot,
        int value)
    {
        provider.Current = new TestState { Value = value };
        manager.ValidateRegistrations();
        manager.SaveToDisk(slot);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(TestState current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }
        public bool ThrowOnCapture { get; set; }

        public TestState CaptureState()
        {
            if (ThrowOnCapture)
                throw new InvalidOperationException("capture failed");

            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }

    private sealed class WrongState
    {
    }

    private sealed class WrongStateTypeSerializer : ISaveSerializer
    {
        public string FileExtension => ".wrong";

        public ISaveMigrationCapableSerializer? Migration => null;

        public ISaveSerializerMetadataHandler? Metadata => null;

        public ISaveSchematic CreateSchematic(Type stateType)
        {
            return new WrongStateTypeSchematic();
        }

        public byte[] Serialize(object data, ISaveSchematic schematic)
        {
            return schematic.SerializeUntyped(data);
        }

        public object Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return schematic.DeserializeUntyped(rawData);
        }

        public int ExtractSchemaVersion(byte[] serializedData)
        {
            return 1;
        }
    }

    private sealed class WrongStateTypeSchematic : ISaveSchematic
    {
        public int SchemaVersion { get; set; } = 1;

        public byte[] SerializeUntyped(object state)
        {
            return Array.Empty<byte>();
        }

        public object DeserializeUntyped(byte[] serialized)
        {
            return new WrongState();
        }
    }
}
