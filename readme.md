# AttunmentsNature

Minimal BepInEx 5 plugin template for [Hadean Tactics](https://store.steampowered.com/app/1324530/Hadean_Tactics/).

Use this repo as a starting point for a new mod. For a more complete example with input handling and an in-game config menu, see [DebuggingAdventures](https://github.com/AZander48/DebuggingAdventures) and [HadeanTacticsExamplesMod](https://github.com/AZander48/HadeanTacticsExamplesMod).

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- An owned copy of Hadean Tactics (Steam)
- [BepInEx 5](https://docs.bepinex.dev/) installed in the game folder

## Setup

1. Create `AttunmentsNaturePath.props` in this folder (it is gitignored) and set your game install path:

```xml
<Project>
  <PropertyGroup>
    <HadeanTacticsDir>C:\Path\To\Hadean Tactics</HadeanTacticsDir>
  </PropertyGroup>
</Project>
```

2. Ensure `AttunmentsNature.csproj` imports that props file:

```xml
<Import Condition="Exists('AttunmentsNaturePath.props')" Project="AttunmentsNaturePath.props" />
```

3. Restore and build:

```powershell
dotnet restore
dotnet build
```

On build, the DLL is copied to `BepInEx/plugins/AttunmentsNature` when `HadeanTacticsDir` is set. A zip is also written to `bin/Publish/AttunmentsNature.zip`.

## Project layout

| File | Purpose |
|------|---------|
| `AttunmentsNature.cs` | Plugin entry point |
| `AttunmentsNature.csproj` | Build config, BepInEx metadata, SDK references |
| `AttunmentsNaturePath.props` | Local game path (not committed) |

## Plugin metadata

Set these in `AttunmentsNature.csproj` before publishing a mod:

```xml
<BepInExPluginGuid>AttunmentsNature</BepInExPluginGuid>
<BepInExPluginName>AttunmentsNature</BepInExPluginName>
<Version>0.1.0</Version>
```

Change the GUID and name for your own mod. BepInEx uses the GUID for the config filename (e.g. `BepInEx/config/AttunmentsNature.cfg`).

## Configuring your mod

Add settings with `Config.Bind(...)` in `Awake()`. They are saved to `BepInEx/config/<your-plugin-guid>.cfg` and can be edited while the game is closed.

**BepInEx Configuration Manager does not work in Hadean Tactics** — its window never appears. Use config files directly, or build your own `OnGUI` menu (see DebuggingAdventures).

## Hadean Tactics modding notes

- The game uses Unity's **new Input System** — legacy `Input.GetKey` and BepInEx `KeyboardShortcut` may not receive key presses. Poll `UnityEngine.InputSystem.Keyboard` from a `MonoBehaviour` instead.
- **Function keys are reserved** by the game (e.g. F9 opens the bug report menu). Use modifier combos like **Ctrl+Shift+M** for mod hotkeys.
- `BaseUnityPlugin.Update()` may not run reliably — attach input/UI logic to a separate `MonoBehaviour` on a `DontDestroyOnLoad` `GameObject`.

## SDK

This template references [NuggetTactics.SDK](https://www.nuget.org/packages/NuggetTactics.SDK) **1.0.2**, which wires up game assembly references and publicization against your local install. It does not redistribute game files.

```powershell
dotnet list package
```

To upgrade the SDK, change the `Version` in `AttunmentsNature.csproj`, then run `dotnet restore --force-evaluate`.

See the [NuggetTactics.SDK README](https://github.com/AZander48/NuggetTactics.SDK) for details.
