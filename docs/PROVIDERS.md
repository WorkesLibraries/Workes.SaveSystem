# Providers

Providers expose application state to the save manager. Each provider owns one stable save key, one current schema version, and the capture/restore behavior for one state type.

## Basic Provider

A provider usually wraps one serializer-friendly state type:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}

public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>
{
    public string SaveKey => "player";
    public int SchemaVersion => 1;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState
    {
        Name = "Rook",
        Level = 5
    };

    public PlayerState CaptureState()
    {
        return Current;
    }

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }
}
```

`SaveKey` is persistent identity. Keep it stable after release.

`SchemaVersion` is the version of this provider's saved payload shape. Start a new provider at `1`, then increase it when old saved files need a migration to load into the current state type.

For example, keep the same schema version when you change only runtime behavior or comments. Increase it when you rename a saved property, split one property into several, change collection structure, change the meaning of a saved value, or make a previously optional value required.

Schema versions are per provider. Changing the `player` payload from version `1` to `2` does not require changing an unrelated `world` or `settings` provider.

`LoadPriority` controls restore order. Lower values load first.

## Registration

Register all providers before saving or loading:

```csharp
manager.RegisterProvider(playerProvider);
manager.RegisterProvider(worldProvider);
manager.ValidateRegistrations();
```

Provider registration is intentionally lightweight. Validation performs the heavier compatibility checks before disk workflows rely on the provider set.

## Provider State Model

Provider state should be a serializer-friendly DTO or object graph. With the built-in JSON serializer, public get/set properties are the normal shape.

Nullable provider state is supported for reference types and `Nullable<T>`, including capture, snapshots, validation, restore, load, and migration-produced null roots.

Keep runtime-only engine objects, services, file handles, caches, and event subscriptions out of provider state. Save DTOs that describe what must be reconstructed instead:

```csharp
public sealed class PlayerState
{
    public string CharacterId { get; set; } = string.Empty;
    public int Level { get; set; }
    public float Health { get; set; }
}
```

If a runtime object needs to be rebuilt after load, store stable IDs or simple values in the state object and rebuild the runtime object in the provider or application layer after restore.

For long-lived saves, treat the state type as a persistence contract. Renaming properties, changing collection shapes, changing nullability expectations, or changing key meanings can require a schema-version bump and migration.

## Lifecycle Hooks

Implement lifecycle contracts when a provider needs callbacks around save/load activity. Keep side effects deterministic and avoid relying on other providers unless load priority makes that relationship explicit.

`ISaveLifecycle.OnBeforeSave()` runs before the provider state is captured. Use it to flush pending runtime values into the DTO that `CaptureState()` returns:

```csharp
public void OnBeforeSave()
{
    Current.Health = healthComponent.CurrentHealth;
}
```

`ISaveLifecycle.OnAfterLoad()` runs after all providers in the restored snapshot have received their state. Use it for post-load rebuild work that should happen after restore, such as refreshing derived caches or notifying runtime systems:

```csharp
public void OnAfterLoad()
{
    RebuildRuntimeCacheFrom(Current);
}
```

Providers are captured and restored in `LoadPriority` order, with lower values first. `OnAfterLoad()` callbacks run after restore work, so do not use them to provide state that another provider must receive during its own `RestoreState(...)` unless the application controls that dependency explicitly.

Validation APIs such as `ValidateSave(...)` inspect loadability without restoring provider state and should not be treated as lifecycle events.

## Default State For Old Saves

When a new provider is added after saves already exist, implement `ISaveDefaultStateProvider<TState>` if the provider can restore a deterministic default for saves that predate it.

Provider manifests identify whether a save is old enough to omit the provider or whether a file is unexpectedly missing from a current save.

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

Use this only when the default is safe and deterministic. An empty quest log, empty tutorial state, or new optional settings object can be reasonable. A provider that owns irreversible progression, inventory, currency, or world state may need a migration or a deliberate compatibility decision instead.

The default-state hook is for old saves whose manifest does not list the provider. It is not a blanket ignore for damaged saves. If the save manifest says the provider file should exist, a missing file still indicates a broken save.

## Provider Sets

Different manager instances can use different provider sets if the application has distinct save scopes. Keep each save root and provider set coherent so saved metadata accurately describes the files in that save.

For example, a profile-level manager might save account settings while a slot-level manager saves gameplay state:

```csharp
var profileManager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: "Saves/Profile");

profileManager.RegisterProvider(settingsProvider);

var slotManager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: "Saves/Slots");

slotManager.RegisterProvider(playerProvider);
slotManager.RegisterProvider(worldProvider);
slotManager.RegisterProvider(questProvider);
```

Avoid changing the registered provider set for the same save root casually. If a provider is removed, renamed, split, or moved to another manager, decide how existing saves should load and document the migration or compatibility behavior.

Memory-only providers can participate in snapshots without being persisted:

```csharp
manager.RegisterMemoryProvider(sessionCacheProvider);
```

They are useful for runtime cache state, editor tooling, test fixtures, or temporary state that should move through `CaptureSnapshot()` and `RestoreSnapshot(...)` but should never be written to disk.

Persisted save roots should still have a clear, stable provider set. That keeps metadata, slot listing, validation, recovery, and missing-file behavior predictable.
