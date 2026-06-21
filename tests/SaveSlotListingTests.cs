using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveSlotListingTests
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
    public void ListSaveSlots_ReturnsEmptyListWhenSaveRootDoesNotExist()
    {
        var manager = CreateManager(Path.Combine(_tempRoot, "missing-root"));

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.Empty);
    }

    [Test]
    public void ListSaveSlots_ReturnsSavedSlotsInOrdinalOrder()
    {
        var manager = CreateManager(_tempRoot);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot-b");
        provider.Current = new TestState { Value = 2 };
        manager.SaveToDisk("slot-a");

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.EqualTo(new[] { "slot-a", "slot-b" }));
    }

    [Test]
    public void ListSaveSlots_ReturnsNestedSavePaths()
    {
        var manager = CreateManager(_tempRoot);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("profile-b/slot-1");
        provider.Current = new TestState { Value = 2 };
        manager.SaveToDisk("profile-a/slot-2");

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.EqualTo(new[] { "profile-a/slot-2", "profile-b/slot-1" }));
    }

    [Test]
    public void ListSaveSlots_IgnoresArtifactsAndFoldersWithoutMetadata()
    {
        var manager = CreateManager(_tempRoot);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");

        CreateFolderWithMetadata("_backup");
        CreateFolderWithMetadata("slot_tmp");
        CreateFolderWithMetadata("slot_toDelete");
        CreateFolderWithMetadata("profile-a/slot_tmp");
        CreateFolderWithMetadata("profile-a/slot_toDelete");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "metadata-less"));

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.EqualTo(new[] { "slot" }));
    }

    [Test]
    public void ListSaveSlots_WithCustomIdentity_ReturnsResolvedSaveNames()
    {
        var manager = new SaveManager<ProfileSlotIdentity>(
            SaveSystemOptions.Create<ProfileSlotIdentity>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                savePathResolver: identity => $"{identity.ProfileId}-{identity.SlotId}"));
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk(new ProfileSlotIdentity("profile-a", "slot-1"));

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.EqualTo(new[] { "profile-a-slot-1" }));
    }

    [Test]
    public void ListSaveSlots_WithBase64JsonSerializerUsesBinMetadataFile()
    {
        var manager = CreateManager(_tempRoot, new Base64JsonSaveSerializer());
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");

        var slots = manager.ListSaveSlots();

        Assert.That(slots, Is.EqualTo(new[] { "slot" }));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.bin")), Is.True);
    }

    private void CreateFolderWithMetadata(string folderName)
    {
        var folderPath = Path.Combine(_tempRoot, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "metadata.json"), "{}");
    }

    private static SaveManager<string> CreateManager(string saveRootPath, ISaveSerializer? serializer = null)
    {
        return new SaveManager<string>(
            SaveSystemOptions.Create(
                saveRootPath: saveRootPath,
                serializer: serializer ?? new JsonSaveSerializer()));
    }

    private readonly struct ProfileSlotIdentity
    {
        public ProfileSlotIdentity(string profileId, string slotId)
        {
            ProfileId = profileId;
            SlotId = slotId;
        }

        public string ProfileId { get; }
        public string SlotId { get; }
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
