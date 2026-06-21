using System;
using System.IO;
using System.Threading;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveMetadataTests
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
    public void ReadSaveMetadata_ReturnsMetadataForSavedSlot()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var metadata = manager.ReadSaveMetadata("slot");

        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.SaveId, Is.Not.Empty);
        Assert.That(metadata.CreatedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(metadata.LastWrittenAtUtc, Is.GreaterThanOrEqualTo(metadata.CreatedAtUtc));
    }

    [Test]
    public void ReadSaveMetadata_PreservesSaveIdAndCreatedTimeAcrossOverwrites()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var firstMetadata = manager.ReadSaveMetadata("slot")!;
        Thread.Sleep(20);

        SaveValue(manager, provider, "slot", 2);
        var secondMetadata = manager.ReadSaveMetadata("slot")!;

        Assert.That(secondMetadata.SaveId, Is.EqualTo(firstMetadata.SaveId));
        Assert.That(secondMetadata.CreatedAtUtc, Is.EqualTo(firstMetadata.CreatedAtUtc));
        Assert.That(secondMetadata.LastWrittenAtUtc, Is.GreaterThan(firstMetadata.LastWrittenAtUtc));
    }

    [Test]
    public void ReadSaveMetadata_WithBinarySerializerUsesBinaryMetadataFile()
    {
        var manager = CreateManager(serializer: new BinarySaveSerializer());
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var metadata = manager.ReadSaveMetadata("slot");
        var metadataPath = Path.Combine(_tempRoot, "slot", "metadata.bin");
        var decodedMetadata = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(metadataPath)));

        Assert.That(metadata, Is.Not.Null);
        Assert.That(File.Exists(metadataPath), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.json")), Is.False);
        Assert.That(decodedMetadata, Does.Contain("\"SaveId\""));
    }

    [Test]
    public void ReadSaveMetadata_ReturnsNullWhenMetadataDoesNotExist()
    {
        var manager = CreateManager();

        var metadata = manager.ReadSaveMetadata("missing");

        Assert.That(metadata, Is.Null);
    }

    [Test]
    public void ReadSaveMetadata_ThrowsWhenMetadataIsInvalid()
    {
        var manager = CreateManager();
        var slotPath = Path.Combine(_tempRoot, "slot");
        Directory.CreateDirectory(slotPath);
        File.WriteAllText(Path.Combine(slotPath, "metadata.json"), "{}");

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ReadSaveMetadata("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to deserialize metadata.json"));
    }

    [Test]
    public void ReadBackupSlotMetadata_ReturnsMetadataForBackupSlot()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var originalMetadata = manager.ReadSaveMetadata("slot")!;
        SaveValue(manager, provider, "slot", 2);

        var backupMetadata = manager.ReadBackupSlotMetadata("slot", slotNumber: 1);

        Assert.That(backupMetadata, Is.Not.Null);
        Assert.That(backupMetadata!.SaveId, Is.EqualTo(originalMetadata.SaveId));
        Assert.That(backupMetadata.CreatedAtUtc, Is.EqualTo(originalMetadata.CreatedAtUtc));
    }

    [Test]
    public void ReadBackupSlotMetadata_ReturnsNullWhenBackupMetadataDoesNotExist()
    {
        var manager = CreateManager();

        var metadata = manager.ReadBackupSlotMetadata("slot", slotNumber: 1);

        Assert.That(metadata, Is.Null);
    }

    [Test]
    public void ReadBackupSlotMetadata_RejectsInvalidSlotNumbers()
    {
        var manager = CreateManager();

        var ex = Assert.Throws<ArgumentException>(() => manager.ReadBackupSlotMetadata("slot", slotNumber: 0));

        Assert.That(ex!.ParamName, Is.EqualTo("slotNumber"));
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        ISaveSerializer? serializer = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer ?? new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
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

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }
}
