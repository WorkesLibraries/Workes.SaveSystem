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
        var bytes = new byte[] { 1, 2, 3 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);

        SaveMigrationStep.SetInt(1, "Level", 12).Migrate(data, factory);
        SaveMigrationStep.SetLong(1, "Gold", 9_000_000_000L).Migrate(data, factory);
        SaveMigrationStep.SetDouble(1, "Speed", 4.5d).Migrate(data, factory);
        SaveMigrationStep.SetDecimal(1, "Cost", 123.45m).Migrate(data, factory);
        SaveMigrationStep.SetString(1, "Name", "Rook").Migrate(data, factory);
        SaveMigrationStep.SetBool(1, "Unlocked", true).Migrate(data, factory);
        SaveMigrationStep.SetBytes(1, "Thumbnail", bytes).Migrate(data, factory);
        SaveMigrationStep.SetDateTime(1, "LastSeen", dateTime).Migrate(data, factory);
        SaveMigrationStep.SetNull(1, "DeletedAt").Migrate(data, factory);

        Assert.That(data.Get("Level").AsInt(), Is.EqualTo(12));
        Assert.That(data.Get("Gold").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(data.Get("Speed").AsDouble(), Is.EqualTo(4.5d));
        Assert.That(data.Get("Cost").AsDecimal(), Is.EqualTo(123.45m));
        Assert.That(data.Get("Name").AsString(), Is.EqualTo("Rook"));
        Assert.That(data.Get("Unlocked").AsBool(), Is.True);
        Assert.That(data.Get("Thumbnail").AsBytes(), Is.EqualTo(bytes));
        Assert.That(data.Get("LastSeen").AsDateTime(), Is.EqualTo(dateTime));
        Assert.That(data.Get("DeletedAt").NodeType, Is.EqualTo(SaveDataNodeType.Null));
    }

    [Test]
    public void AddDefault_NewPrimitiveHelpersAddOnlyWhenFieldsAreMissing()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        var bytes = new byte[] { 1, 2, 3 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);
        data.Set("Long", factory.CreateLong(1L));
        data.Set("Double", factory.CreateDouble(1d));
        data.Set("Decimal", factory.CreateDecimal(1m));
        data.Set("Bytes", factory.CreateBytes(new byte[] { 9 }));
        data.Set("DateTime", factory.CreateDateTime(DateTime.MinValue));

        SaveMigrationStep.AddLongDefault(1, "Long", 9_000_000_000L).Migrate(data, factory);
        SaveMigrationStep.AddDoubleDefault(1, "Double", 4.5d).Migrate(data, factory);
        SaveMigrationStep.AddDecimalDefault(1, "Decimal", 123.45m).Migrate(data, factory);
        SaveMigrationStep.AddBytesDefault(1, "Bytes", bytes).Migrate(data, factory);
        SaveMigrationStep.AddDateTimeDefault(1, "DateTime", dateTime).Migrate(data, factory);
        SaveMigrationStep.AddLongDefault(1, "MissingLong", 9_000_000_000L).Migrate(data, factory);
        SaveMigrationStep.AddDoubleDefault(1, "MissingDouble", 4.5d).Migrate(data, factory);
        SaveMigrationStep.AddDecimalDefault(1, "MissingDecimal", 123.45m).Migrate(data, factory);
        SaveMigrationStep.AddBytesDefault(1, "MissingBytes", bytes).Migrate(data, factory);
        SaveMigrationStep.AddDateTimeDefault(1, "MissingDateTime", dateTime).Migrate(data, factory);

        Assert.That(data.Get("Long").AsLong(), Is.EqualTo(1L));
        Assert.That(data.Get("Double").AsDouble(), Is.EqualTo(1d));
        Assert.That(data.Get("Decimal").AsDecimal(), Is.EqualTo(1m));
        Assert.That(data.Get("Bytes").AsBytes(), Is.EqualTo(new byte[] { 9 }));
        Assert.That(data.Get("DateTime").AsDateTime(), Is.EqualTo(DateTime.MinValue));
        Assert.That(data.Get("MissingLong").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(data.Get("MissingDouble").AsDouble(), Is.EqualTo(4.5d));
        Assert.That(data.Get("MissingDecimal").AsDecimal(), Is.EqualTo(123.45m));
        Assert.That(data.Get("MissingBytes").AsBytes(), Is.EqualTo(bytes));
        Assert.That(data.Get("MissingDateTime").AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void From_ComposesNewPrimitiveActionHelpers()
    {
        var factory = CreateJsonFactory();
        var data = factory.CreateObject();
        var bytes = new byte[] { 1, 2, 3 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);

        var migration = SaveMigrationStep.From(
            1,
            SaveMigrationStep.SetLong("Long", 9_000_000_000L),
            SaveMigrationStep.SetDouble("Double", 4.5d),
            SaveMigrationStep.SetDecimal("Decimal", 123.45m),
            SaveMigrationStep.SetBytes("Bytes", bytes),
            SaveMigrationStep.SetDateTime("DateTime", dateTime));

        migration.Migrate(data, factory);

        Assert.That(data.Get("Long").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(data.Get("Double").AsDouble(), Is.EqualTo(4.5d));
        Assert.That(data.Get("Decimal").AsDecimal(), Is.EqualTo(123.45m));
        Assert.That(data.Get("Bytes").AsBytes(), Is.EqualTo(bytes));
        Assert.That(data.Get("DateTime").AsDateTime(), Is.EqualTo(dateTime));
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
