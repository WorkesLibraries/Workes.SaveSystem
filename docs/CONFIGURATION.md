# Configuration

`SaveSystemOptions<TIdentity>` controls how a `SaveManager<TIdentity>` resolves save paths, file names, serializer behavior, warnings, and missing provider files.

## Default Managers

Use `CreateDefault` for simple applications:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: "Saves");
```

The save root belongs to the application. Engine projects should pass an engine-owned persistent-data path.

## Explicit Options

Use `SaveSystemOptions.Create(...)` when the manager needs custom path, file, warning, or missing-file behavior:

```csharp
var options = SaveSystemOptions.Create<ProfileSlotIdentity>(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(JsonSaveFormatting.Compact),
    savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId),
    fileNameResolver: context => context.SaveKey,
    missingProviderFileBehavior: MissingProviderFileBehavior.Throw,
    warningSink: message => logger.Warn(message));

var manager = new SaveManager<ProfileSlotIdentity>(options);
```

For simple string slots, omit `savePathResolver`; string identities resolve directly to relative save paths.

## Save Paths And File Names

Keep custom `fileNameResolver` output stable over time. Do not include `SchemaVersion` in file names unless you intentionally want to break old saves.

Provider schema versions belong inside payloads so the migration system can find and migrate older files.

The file-name resolver receives a `SaveFileContext`:

| Property | Meaning |
|---|---|
| `SaveKey` | Provider save key. |
| `SchemaVersion` | Current provider schema version. |
| `SerializerType` | Active serializer type. |

The default file-name resolver uses only `SaveKey`.

`tempFolderName` controls the temporary folder suffix used during atomic save writes. The default is `_tmp`. Keep custom values as simple path segments, not full paths.

`warningSink` lets applications route save-system warnings into their own logging system:

```csharp
var options = SaveSystemOptions.Create(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    warningSink: message => logger.Warn(message));
```

## Missing Provider Files

`MissingProviderFileBehavior` controls how strictly the manager treats registered providers whose files are missing from a save.

This matters when the registered provider set changes over time. A missing file can mean either:

- the save is old and was written before the provider existed.
- the save is current, but a provider file was deleted, moved, renamed, or corrupted.

Provider manifests let the loader distinguish those cases for saves written by manifest-aware versions of the package. If the manifest says a provider file should exist, the missing file is treated as a problem. If the manifest shows the save predates a newly registered provider, the provider may be restored from deterministic default state instead.

Use strict missing-file behavior when every registered provider file must be present:

```csharp
var options = SaveSystemOptions.Create(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    missingProviderFileBehavior: MissingProviderFileBehavior.Throw);
```

Use `ISaveDefaultStateProvider<TState>` when a newly added provider can safely load old saves with a known default:

```csharp
public sealed class QuestSaveProvider :
    ISaveProvider<QuestState>,
    ISaveDefaultStateProvider<QuestState>
{
    public string SaveKey => "quests";
    public int SchemaVersion => 1;
    public int LoadPriority => 10;

    public QuestState Current { get; set; } = new QuestState();

    public QuestState CaptureState()
    {
        return Current;
    }

    public void RestoreState(QuestState state)
    {
        Current = state;
    }

    public QuestState CreateDefaultStateForMissingSave()
    {
        return new QuestState();
    }
}
```

In that example, an old save that predates the `quests` provider can restore an empty quest state. A save whose manifest already listed `quests` but whose `quests.json` file is missing should still be treated as damaged.

## Validation

Call `ValidateRegistrations()` after registering or unregistering providers and before disk save/load workflows:

```csharp
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Validation catches provider key problems, serializer write compatibility, migration setup issues, and file-name collisions at the setup point you choose.

Validation is not a full future-load proof. Problems that only appear while reading real saved data can still surface during load.

## Slot Listing And Cleanup

Use `ListSaveSlots()` for save/load menus:

```csharp
IReadOnlyList<string> slots = manager.ListSaveSlots();
```

Returned slot names are resolved relative save paths, not `TIdentity` values, because custom identity resolvers may not be reversible. The list is sorted with ordinal string ordering and ignores backup, temp, to-delete, and metadata-less folders.

Use `DeleteSave(...)`, `DeleteBackupSlot(...)`, and `DeleteAllBackupSlots(...)` for save-menu cleanup or debug tooling.

For the full manager operation surface, including existence checks, validation results, try-load statuses, backup operations, and snapshots, see [Save Operations](SAVE_OPERATIONS.md).
