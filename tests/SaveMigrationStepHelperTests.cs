using System;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveMigrationStepHelperTests
{
    [Test]
    public void AddDefault_AddsOnlyWhenFieldIsMissing()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("Level", factory.CreateInt(3));
        var addExisting = SaveMigrationStep.AddIntDefault(1, "Level", 12);
        var addMissing = SaveMigrationStep.AddStringDefault(1, "Name", "Scout");

        addExisting.Migrate(data, factory);
        addMissing.Migrate(data, factory);

        Assert.That(data.Get("Level").AsInt(), Is.EqualTo(3));
        Assert.That(data.Get("Name").AsString(), Is.EqualTo("Scout"));
    }

    [Test]
    public void From_RunsMigrationActionsInOrder()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("XP", factory.CreateInt(10));

        var migration = SaveMigrationStep.From(
            1,
            SaveMigrationStep.Rename("XP", "Experience"),
            SaveMigrationStep.SetInt("Level", 3));

        migration.Migrate(data, factory);

        Assert.That(data.Has("XP"), Is.False);
        Assert.That(data.Get("Experience").AsInt(), Is.EqualTo(10));
        Assert.That(data.Get("Level").AsInt(), Is.EqualTo(3));
    }

    [Test]
    public void SetHelpers_ReplacePrimitiveValues()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("Level", factory.CreateInt(3));
        data.Set("Name", factory.CreateString("Scout"));

        SaveMigrationStep.SetInt(1, "Level", 12).Migrate(data, factory);
        SaveMigrationStep.SetString(1, "Name", "Rook").Migrate(data, factory);
        SaveMigrationStep.SetBool(1, "Unlocked", true).Migrate(data, factory);
        SaveMigrationStep.SetNull(1, "DeletedAt").Migrate(data, factory);

        Assert.That(data.Get("Level").AsInt(), Is.EqualTo(12));
        Assert.That(data.Get("Name").AsString(), Is.EqualTo("Rook"));
        Assert.That(data.Get("Unlocked").AsBool(), Is.True);
        Assert.That(data.Get("DeletedAt").NodeType, Is.EqualTo(SaveDataNodeType.Null));
    }

    [Test]
    public void Remove_RemovesFieldWhenPresent()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("LegacyName", factory.CreateString("Scout"));

        SaveMigrationStep.Remove(1, "LegacyName").Migrate(data, factory);

        Assert.That(data.Has("LegacyName"), Is.False);
    }

    [Test]
    public void Rename_MovesExistingField()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("XP", factory.CreateInt(42));

        SaveMigrationStep.Rename(1, "XP", "Experience").Migrate(data, factory);

        Assert.That(data.Has("XP"), Is.False);
        Assert.That(data.Get("Experience").AsInt(), Is.EqualTo(42));
    }

    [Test]
    public void Rename_RejectsExistingTargetUnlessOverwriteIsEnabled()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        data.Set("XP", factory.CreateInt(42));
        data.Set("Experience", factory.CreateInt(10));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SaveMigrationStep.Rename(1, "XP", "Experience").Migrate(data, factory));

        Assert.That(ex!.Message, Does.Contain("target field already exists"));

        SaveMigrationStep.Rename(1, "XP", "Experience", overwrite: true).Migrate(data, factory);

        Assert.That(data.Has("XP"), Is.False);
        Assert.That(data.Get("Experience").AsInt(), Is.EqualTo(42));
    }

    [Test]
    public void HelperFactories_RejectInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => SaveMigrationStep.SetInt(1, " ", 1));
        Assert.Throws<ArgumentNullException>(() => SaveMigrationStep.Set(1, "Level", null!));
        Assert.Throws<ArgumentException>(() => SaveMigrationStep.From(1));
    }

    private static ISaveDataNodeFactory CreateJsonFactory()
    {
        return new JsonSaveSerializer().NodeFactory;
    }
}
