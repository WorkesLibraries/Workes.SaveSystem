# Migrations

Migrations let providers and application metadata load old saved payloads after state shape changes.

The migration system is based on three ideas:

- each saved payload has a schema version.
- each migration step upgrades exactly one version, such as `1 -> 2`.
- migration steps edit serializer-neutral `ISaveDataNode` trees instead of serializer-specific JSON or binary objects.

The built-in JSON serializer supports migrations. Serializer wrappers such as compression and transforms forward migration support from their inner serializer.

The examples assume the usual project references and using directives for the package, plus `System.Collections.Generic` when snippets use `IReadOnlyList<T>` or `List<T>`.

## When To Add A Migration

Add a migration when an old saved payload would not deserialize correctly into the current state type.

Common migration triggers:

- a saved property was renamed.
- one saved field was split into multiple fields.
- several fields were merged.
- a required field was added.
- a collection item shape changed.
- a dictionary key convention changed.
- a root type changed, such as `int` to `string`.
- application metadata changed shape.

You usually do not need a migration for runtime-only code changes, comments, methods, or fields that are not serialized.

## Provider Schema Versions

Each provider exposes its current payload version:

```csharp
public int SchemaVersion => 2;
```

Start a new provider at version `1`. Increase the version when old saved files need migration to the current state type.

Schema versions are per provider. A `player` provider can move from version `1` to `2` without changing the version of a `world` provider.

Do not put schema versions in file names unless you intentionally want to break old saves. The migration system reads versions from the serialized payload.

## Basic Provider Migration

Suppose version 1 saved only a player name:

```csharp
public sealed class PlayerStateV1
{
    public string Name { get; set; } = string.Empty;
}
```

Version 2 adds a required `Level` field:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}
```

The current provider implements `ISaveMigratable` and returns a migration source:

```csharp
public sealed class PlayerSaveProvider :
    ISaveProvider<PlayerState>,
    ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState();

    public PlayerState CaptureState()
    {
        return Current;
    }

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }

    public ISaveMigrationSource CreateMigrationSource()
    {
        return new PlayerMigrationSource();
    }
}
```

The migration source provides the steps:

```csharp
public sealed class PlayerMigrationSource : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            SaveMigrationStep.AddIntDefault(
                fromVersion: 1,
                key: "Level",
                value: 1)
        };
}
```

When the manager loads a version 1 `player` payload into this version 2 provider, it adds `Level = 1` before deserializing the current `PlayerState`.

## Migration Step Rules

Each `SaveMigrationStep` migrates from `FromVersion` to `FromVersion + 1`.

For a provider at schema version `4`, loading a version `1` payload requires all of these steps:

- `1 -> 2`
- `2 -> 3`
- `3 -> 4`

The manager applies steps sequentially. If any step is missing, duplicate, null, or throws during execution, migration fails.

Registration validation catches setup problems such as duplicate or null migration steps. Missing historical migration paths are discovered when an older saved slot is validated with `ValidateSave(...)` or loaded.

## Composing Multiple Edits

Use `SaveMigrationStep.From(...)` when one version bump needs several edits:

```csharp
SaveMigrationStep.From(
    1,
    SaveMigrationStep.Rename("XP", "Experience"),
    SaveMigrationStep.AddIntDefault("Level", 1),
    SaveMigrationStep.Remove("LegacyFlag"))
```

The actions run in the order provided.

## Built-In Step Helpers

The helper methods work on object-root fields. They are useful for common DTO migrations.

| Helper | Behavior |
|---|---|
| `AddDefault(...)` | Adds a field only when it does not already exist. |
| `Set(...)` | Sets or replaces a field. |
| `Remove(...)` | Removes a field if it exists. |
| `Rename(...)` | Renames a field if the source exists. |
| `Move(...)` | Alias for `Rename(...)`. |

Typed helpers exist for common primitive values:

```csharp
SaveMigrationStep.AddIntDefault(1, "Level", 1);
SaveMigrationStep.AddStringDefault(1, "Title", "Unknown");
SaveMigrationStep.AddBoolDefault(1, "Unlocked", false);
SaveMigrationStep.SetLong(1, "Gold", 9_000_000_000L);
SaveMigrationStep.SetDouble(1, "Speed", 4.5d);
SaveMigrationStep.SetDecimal(1, "Cost", 123.45m);
SaveMigrationStep.SetBytes(1, "Thumbnail", bytes);
SaveMigrationStep.SetDateTime(1, "LastSeen", lastSeenUtc);
SaveMigrationStep.SetNull(1, "DeletedAt");
```

Most helpers have two forms:

- step form: `SaveMigrationStep.SetInt(fromVersion, key, value)`
- action form: `SaveMigrationStep.SetInt(key, value)`

Use the step form for a one-action migration. Use the action form inside `SaveMigrationStep.From(...)`.

## Rename And Overwrite

`Rename(...)` and `Move(...)` do nothing when the source field does not exist.

By default, they throw if the target field already exists:

```csharp
SaveMigrationStep.Rename(1, "XP", "Experience");
```

Allow replacement explicitly:

```csharp
SaveMigrationStep.Rename(
    fromVersion: 1,
    oldKey: "XP",
    newKey: "Experience",
    overwrite: true);
```

Use `overwrite: true` only when losing the existing target value is intentional.

## Data Nodes

`ISaveDataNode` is the editable migration view of a serialized payload.

The node root is the provider state itself, not the serializer envelope. For JSON, migration steps edit the value under the payload's `Data` field while the serializer keeps `SchemaVersion` envelope handling internal.

Supported node types:

- `Object`
- `Array`
- `Int`
- `Long`
- `Float`
- `Double`
- `Decimal`
- `String`
- `Bool`
- `Bytes`
- `DateTime`
- `Null`

Use `node.NodeType` when code needs the exact `SaveDataNodeType` value. Use `IsObject()`, `IsArray()`, and `IsNull()` for common shape checks.

Object operations:

```csharp
if (root.Has("Name"))
{
    var name = root.Get("Name").AsString();
    root.Set("DisplayName", factory.CreateString(name));
    root.Remove("Name");
}
```

Array operations:

```csharp
for (var i = 0; i < root.Count; i++)
{
    var item = root.GetAt(i);
    item.Set("Count", factory.CreateInt(1));
}
```

Primitive operations:

```csharp
var level = root.Get("Level").AsInt();
root.Get("Level").SetInt(level + 1);
root.Set("DeletedAt", factory.CreateNull());
```

Root replacement:

```csharp
root.ReplaceWith(factory.CreateString("level-" + root.AsInt()));
```

Use `ReplaceWith(...)` when the root shape changes, such as primitive to object, primitive to string, array to object, or object to null.

## Node Factory Rule

Create new nodes with the `ISaveDataNodeFactory` passed to the migration action:

```csharp
new SaveMigrationStep(1, (root, factory) =>
{
    root.Set("Level", factory.CreateInt(1));
});
```

Do not reuse nodes created by another serializer or another factory instance. Nodes can only be combined with nodes from the same factory that owns the migration tree.

## Root Object Example

Version 1:

```csharp
public sealed class PlayerStateV1
{
    public string Name { get; set; } = string.Empty;
}
```

Version 2:

```csharp
public sealed class PlayerState
{
    public string DisplayName { get; set; } = string.Empty;
    public int Level { get; set; }
}
```

Migration:

```csharp
SaveMigrationStep.From(
    1,
    SaveMigrationStep.Rename("Name", "DisplayName"),
    SaveMigrationStep.AddIntDefault("Level", 1))
```

## Root List Example

Version 1 item state:

```csharp
public sealed class LegacyItemState
{
    public string Id { get; set; } = string.Empty;
}
```

Version 2 item state:

```csharp
public sealed class ItemState
{
    public string Id { get; set; } = string.Empty;
    public int Count { get; set; }
}
```

Provider state can be a list:

```csharp
public sealed class InventoryProvider :
    ISaveProvider<List<ItemState>>,
    ISaveMigratable
{
    public string SaveKey => "inventory";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public List<ItemState> Current { get; set; } = new List<ItemState>();

    public List<ItemState> CaptureState() => Current;

    public void RestoreState(List<ItemState> state)
    {
        Current = state;
    }

    public ISaveMigrationSource CreateMigrationSource()
    {
        return new InventoryMigrationSource();
    }
}
```

Migration:

```csharp
public sealed class InventoryMigrationSource : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            new SaveMigrationStep(1, (root, factory) =>
            {
                for (var i = 0; i < root.Count; i++)
                {
                    var item = root.GetAt(i);

                    if (!item.Has("Count"))
                        item.Set("Count", factory.CreateInt(1));
                }
            })
        };
}
```

## Root Dictionary Example

For dictionary roots, object keys are dictionary keys:

```csharp
new SaveMigrationStep(1, (root, _) =>
{
    if (!root.Has("old-key"))
        return;

    var value = root.Get("old-key");
    root.Remove("old-key");
    root.Set("new-key", value);
});
```

## Root Primitive Or Null Example

Migration nodes can represent primitive or null roots. Use `ReplaceWith(...)` to change the root:

```csharp
new SaveMigrationStep(1, (root, factory) =>
{
    root.ReplaceWith(factory.CreateString("level-" + root.AsInt()));
});
```

To migrate a primitive root to null:

```csharp
new SaveMigrationStep(1, (root, factory) =>
{
    root.ReplaceWith(factory.CreateNull());
});
```

## Application Metadata Migrations

Application metadata uses the same migration step model as provider payloads.

Increase `MetadataSchemaVersion` when old application metadata needs migration, then implement `ISaveMetadataMigratable`:

```csharp
public sealed class SaveMenuMetadataProvider :
    ISaveMetadataProvider<SaveMenuMetadata>,
    ISaveMetadataMigratable
{
    public int MetadataSchemaVersion => 2;

    public SaveMenuMetadata Current { get; set; } = new SaveMenuMetadata();

    public SaveMenuMetadata CaptureMetadata()
    {
        return Current;
    }

    public void RestoreMetadata(SaveMenuMetadata metadata)
    {
        Current = metadata;
    }

    public ISaveMigrationSource CreateMetadataMigrationSource()
    {
        return new SaveMenuMetadataMigrationSource();
    }
}
```

```csharp
public sealed class SaveMenuMetadataMigrationSource : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            SaveMigrationStep.AddIntDefault(
                fromVersion: 1,
                key: "PlaytimeSeconds",
                value: 0)
        };
}
```

Application metadata can have object, list, dictionary, primitive, or null roots just like provider payloads.

## Serializer Requirements

Migrations require serializer migration support.

The active serializer must provide `ISaveSerializer.Migration`. For application metadata migrations, the serializer must also support inline application metadata through `ISaveApplicationMetadataSerializer`.

The built-in `JsonSaveSerializer` supports provider migrations and application metadata migrations. Compression and transform serializers preserve support when their inner serializer supports it.

Registration validation catches unsupported serializer capabilities before disk operations.

## Load And Validation Failures

When migration cannot be completed, `TryLoadFromDisk(...)` reports `SaveLoadStatus.MigrationFailed`.

Throwing load methods raise an `InvalidOperationException` with diagnostic text.

Common migration failure causes:

- saved version is newer than the current provider version.
- no migration source is available.
- a migration step is missing.
- duplicate migration steps exist for the same `FromVersion`.
- a migration step throws.
- the serializer cannot parse the old payload into migration nodes.
- the migrated node cannot be serialized back to the payload format.

Exception messages are for humans and logs. Branch on `SaveLoadStatus` for expected failure handling.

## Testing Migrations

Test migrations with representative old payloads.

Recommended tests:

- write a version 1 save, load it with a version 2 provider, and assert restored state.
- cover every step in a multi-version path, such as `1 -> 2 -> 3`.
- test missing migration paths report `MigrationFailed`.
- test root list, dictionary, primitive, or null migrations when used.
- test application metadata migrations separately from provider migrations.
- validate that failed migrations do not partially restore provider state.

## Long-Lived Save Checklist

- Keep provider save keys stable.
- Keep resolved provider file names stable.
- Keep application metadata provider meaning stable per manager.
- Increase schema versions when payloads require migration.
- Add migration steps in the same work as save-shape changes.
- Keep migrations deterministic.
- Test old representative save payloads when changing persistence models.
- Document intentionally breaking save-format changes in application release notes.

## Determinism Rules

Migration steps should be deterministic and based on the old payload plus constants owned by the migration.

Avoid:

- current time
- random values
- engine state
- other providers' runtime state
- service calls
- filesystem reads outside the save being migrated

If a value cannot be derived deterministically, choose a stable compatibility default and document that decision.
