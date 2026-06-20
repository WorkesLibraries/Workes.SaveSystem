using System;
using System.Linq;
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
    public void SaveSnapshot_Add_RejectsNullState()
    {
        var snapshot = new SaveSnapshot();

        var ex = Assert.Throws<ArgumentNullException>(() => snapshot.Add("player", 1, null!));

        Assert.That(ex!.ParamName, Is.EqualTo("state"));
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

}
