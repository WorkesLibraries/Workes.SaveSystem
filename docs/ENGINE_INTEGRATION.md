# Unity And Godot Integration

`Workes.SaveSystem` is engine-neutral. Engine projects provide the save root path and decide when save/load operations run.

## Unity

Use a Unity-owned persistent data path:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: Application.persistentDataPath);
```

Register providers owned by game systems, then validate before save/load workflows.

Avoid storing Unity engine objects directly in provider state. Save serializer-friendly DTOs instead, such as IDs, numbers, strings, and plain state objects.

## Godot

Use a Godot-owned user data path:

```csharp
var saveRootPath = ProjectSettings.GlobalizePath("user://Saves");

var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath);
```

Restore DTO state into engine nodes or resources after load. Keep engine object references out of serialized provider DTOs.

## Engine Timing

Applications decide when save/load occurs. Common choices include:

- manual save slots
- autosave checkpoints
- scene transition saves
- profile or character selection screens
- debug tooling

Keep provider capture and restore behavior deterministic. If providers depend on engine lifecycle order, reflect that through registration and load priorities.

## Paths And Permissions

Use directories the engine documents as writable. Do not assume the application can write beside the executable, especially after packaging or on mobile platforms.

If an engine has asynchronous file APIs or platform save systems, wrap `Workes.SaveSystem` at the application layer rather than adding engine-specific assumptions to provider state.
