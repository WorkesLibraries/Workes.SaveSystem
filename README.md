# Workes.SaveSystem

[![NuGet](https://img.shields.io/nuget/v/Workes.SaveSystem.svg)](https://www.nuget.org/packages/Workes.SaveSystem)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/WorkesLibraries/Workes.SaveSystem/blob/main/LICENSE)

`Workes.SaveSystem` is an engine-neutral .NET save system for games and applications that need registered state providers, disk saves, backups, metadata, and save-format migrations.

The package is not tied to Unity, Godot, or another engine. Your application chooses the persistent save path and serializer, then `SaveManager<TIdentity>` coordinates provider capture, persistence, loading, validation, recovery, and migration.

## Highlights

- Register typed state providers with stable save keys and schema versions.
- Save and load named slots using string or custom identity types.
- Use readable JSON by default, compact JSON when desired, or serializer wrappers for compression and transforms.
- Rotate backups and recover from interrupted writes.
- Store typed application metadata for save menus without loading provider files.
- Migrate old provider payloads through format-neutral data nodes.
- Distinguish old saves from missing provider files through persisted provider manifests.
- Extend serialization, metadata, transforms, migrations, provider lifecycle, and default-state behavior.

## Installation

Install from NuGet:

```bash
dotnet add package Workes.SaveSystem --version 1.0.1
```

Or add a package reference:

```xml
<PackageReference Include="Workes.SaveSystem" Version="1.0.1" />
```

The package targets .NET Standard 2.1 and currently depends on `Newtonsoft.Json` for the built-in JSON serializer.

## Quick Example

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");

var playerProvider = new PlayerSaveProvider();
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();

manager.SaveToDisk("slot-1");

playerProvider.Current = new PlayerState();
manager.LoadFromDisk("slot-1");

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

    public PlayerState CaptureState() => Current;

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }
}
```

See the [Quick Start](docs/QUICK_START.md) for the beginner-first walkthrough.

## Documentation

Start here:

1. [Quick Start](docs/QUICK_START.md)
2. [Configuration](docs/CONFIGURATION.md)
3. [Providers](docs/PROVIDERS.md)

Focused guides:

- [Serialization](docs/SERIALIZATION.md)
- [Save Operations](docs/SAVE_OPERATIONS.md)
- [Save Metadata](docs/METADATA.md)
- [Backups And Recovery](docs/BACKUPS_AND_RECOVERY.md)
- [Migrations](docs/MIGRATIONS.md)
- [Extending The Save System](docs/EXTENDING.md)
- [Unity And Godot Integration](docs/ENGINE_INTEGRATION.md)

See the [Changelog](CHANGELOG.md) for release history and migration-sensitive changes.

## Companion Packages

The core package ships JSON and does not reference MessagePack. Compact MessagePack saves are handled by the optional companion package:

- [Workes.SaveSystem.MessagePack](https://github.com/WorkesLibraries/Workes.SaveSystem.MessagePack)

Use JSON when readable save files are useful. Use compressed JSON for smaller files without an additional dependency. Use the MessagePack companion package when compact binary saves are more important than direct readability.

## License

Workes.SaveSystem is available under the [MIT License](LICENSE).
