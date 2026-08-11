# Quick Start

This guide takes a new project from package installation to saving and loading one slot.

## Prerequisites

You need:

- a .NET project compatible with .NET Standard 2.1.
- the .NET SDK or another NuGet-capable development environment.
- a folder path where the application is allowed to write saves.

The examples use `string` save-slot identities and top-level C# statements.

## Install The Package

From the project directory:

```bash
dotnet add package Workes.SaveSystem
```

The NuGet package ID is `Workes.SaveSystem`.

## Create A Save Manager

Create a serializer and a save manager:

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");
```

`SaveManager<string>` means save identities are strings such as `"slot-1"`. The save root path is application-owned. In an engine project, pass the engine's persistent-data path instead of a hard-coded relative path.

## Define Save State

Create a serializable state type:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}
```

The built-in JSON serializer works with ordinary public get/set properties.

## Create A Provider

Providers own one named piece of save state:

```csharp
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

Use a stable `SaveKey`. It becomes part of the saved file layout and should not be renamed casually.

## Register And Validate

Register providers before saving or loading:

```csharp
var playerProvider = new PlayerSaveProvider();

manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Validation checks provider keys, file-name collisions, serializer compatibility, and migration setup before disk operations depend on them.

## Save And Load

Save a slot:

```csharp
manager.SaveToDisk("slot-1");
```

Change runtime state, then load the slot:

```csharp
playerProvider.Current = new PlayerState();

var loaded = manager.LoadFromDisk("slot-1");

Console.WriteLine(loaded); // True
Console.WriteLine(playerProvider.Current.Name); // Rook
Console.WriteLine(playerProvider.Current.Level); // 5
```

## Files Written

With the default JSON serializer and a provider key of `player`, the save folder contains files like:

```text
Saves/
  slot-1/
    metadata.json
    player.json
```

`metadata.json` stores package metadata such as provider manifests and timestamps. `player.json` stores the provider payload.

## Where To Go Next

Recommended reading:

1. [Configuration](CONFIGURATION.md) explains save roots, identity paths, file names, missing-provider behavior, and warnings.
2. [Providers](PROVIDERS.md) covers provider contracts, lifecycle hooks, default state, and provider sets.
3. [Save Operations](SAVE_OPERATIONS.md) covers save, load, validate, list, metadata, delete, backup, and snapshot APIs.

Focused guides:

- [Serialization](SERIALIZATION.md)
- [Save Metadata](METADATA.md)
- [Backups And Recovery](BACKUPS_AND_RECOVERY.md)
- [Migrations](MIGRATIONS.md)
- [Extending The Save System](EXTENDING.md)
- [Unity And Godot Integration](ENGINE_INTEGRATION.md)
