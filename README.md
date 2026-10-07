# MRP: dead `Resource.changed` callables after C# hot reload

Minimal reproduction for the errors the editor prints after every assembly
reload when a `[Notify]` property of a Godot script class holds a `Resource`
(or an `Array<Resource>`).

The generated setter subscribes to the nested resource's `changed` signal with
a compiler-generated closure, which Godot's hot-reload callable serialization
cannot serialize. The engine then releases the delegate handle while leaving
the native signal connection in place, so the connection is dead but stays
connected. During the reload, restoring the resource's own `[Notify]`
properties emits `changed`, which hits the dead callables and floods stderr.

## Layout

| Path | What it is |
|---|---|
| `Game/` | Godot project. `project.godot` + `Game.csproj` (`Godot.NET.Sdk` 4.7.2, `GodotSharp.SourceGenerators` 2.7.1). |
| `Game/NotifyGadget.cs` | `[Tool] Resource` with `[Export][Notify] int Level`. Its restore path emits `changed` during the reload. |
| `Game/NotifyWidget.cs` | `[Tool] Node` with `[Export][Notify] NotifyGadget Gadget` and `[Export][Notify] Array<NotifyGadget> Gadgets`. Its generated setters subscribe to the nested resource's `changed`. |
| `Game/gadget.tres` | `NotifyGadget` instance (`Level = 42`) assigned to both widget properties in the scene. |
| `Game/main.tscn` | Scene opened in the editor; it must stay open so live script instances exist during the reload. |

## Reproduce

Requires the official editor **4.7.2-stable** (mono), on Windows.

1. Build once so the editor finds an up-to-date assembly:
   ```
   dotnet build Game/Game.csproj
   ```
2. Open the project in the editor with `main.tscn`:
   ```
   Godot_v4.7.2-stable_mono_win64_console.exe --path Game --editor res://main.tscn
   ```
3. With the editor still running, rebuild twice from a terminal (each rebuild
   hot-reloads the assembly):
   ```
   dotnet build Game/Game.csproj --no-incremental
   ```

### Expected (broken) output

After each reload the editor stderr prints, per dead connection:

```
ERROR: Parameter "delegate_handle.value" is null.
   at: call (modules/mono/managed_callable.cpp:96)
       [1] Godot.Error Godot.GodotObject.EmitSignal(Godot.StringName, Godot.Variant[]) (...)
ERROR: Can't get method on CallableCustom "Delegate::Invoke".
ERROR: Error calling from signal 'changed' to callable: 'ManagedCallableMiddleman::': Method not found.
   at: emit_signalp (core/object/object.cpp:1311)
```

The managed backtrace points directly at the generated notify code:

```
[2] void NotifyGadget+__GodotNotifyLevel.<Set>g__OnDataChanged|14_1(...) (.../NotifyGadget.Level.g.cs:70)
[3] void NotifyGadget+__GodotNotifyLevel.Set(int, System.Action) (.../NotifyGadget.Level.g.cs:55)
[4] void NotifyGadget.set_Level(int) (.../NotifyGadget.cs:15)
[5] void NotifyGadget.RestoreGodotObjectData(...) (.../NotifyGadget_ScriptSerialization.generated.cs:19)
```

Measured on the versions above, two reloads per editor session:

| Package | stderr size | `delegate_handle.value is null` | `Method not found` | `signal 'changed'` errors | `csharp_script.cpp` serialize guard |
|---|---|---|---|---|---|
| `2.7.1` (nuget.org) | 16,979 B | 3 | 3 | 3 | 1 |
| patched generator build | 0 B | 0 | 0 | 0 | 0 |

## Verifying the fix

The proposed fix replaces the compiler-generated closure with a method group on
the outer `GodotObject` (`__OnNotify…NestedChanged`), which the engine can
serialize and restore, and guards the subscription with `IsConnected`. Build the
generator from the patched branch, drop the `.nupkg` into a local NuGet source,
point `Game.csproj` at that version and repeat the steps above: stderr stays
empty while both reloads still happen (`Assembly load context unloaded
successfully.` appears twice on stdout).

## Notes

* The errors only appear **after a reload**, never on a cold start: on first
  load the generated setters run on the live instance, while a reload
  serializes the managed callables first and only then restores the resources.
* The scene must be open in the editor; otherwise there are no live script
  instances to connect to the resource's `changed` signal.
* SDK / package versions in `Game.csproj` must match the editor release under
  test.
