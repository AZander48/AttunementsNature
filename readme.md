# AttunmentsNature

BepInEx 5 plugin for [Hadean Tactics](https://store.steampowered.com/app/1324530/Hadean_Tactics/).

Adds **attunment** buffs: a unit’s attacks apply **burn**, **decay**, **forstbite**, or **shock** based on a fixed element. Buff strength equals the status value; each attack reduces that value by **10%** (minimum 1). Forstbite duration scales **1:1** with value.

## What it does

| Feature | Behavior |
|---------|----------|
| Attunment buff | Enchant on a unit; attacks apply one fixed ailment |
| Elements | `burn`, `decay`, `forstbite`, `shock` |
| Decay on attack | Value × 0.1 per attack (ceil, min 1) |
| Test card | Ally-target card that applies the configured element |
| Hero | Custom hero whose mana skill self-applies attunment |

Implementation uses `PoisonClaw` as a carrier with `args = "attunment:{element}"`, plus Harmony hooks on melee, projectiles, and status duration.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- Hadean Tactics (Steam)
- [BepInEx 5](https://docs.bepinex.dev/) in the game folder

## Setup

1. Create `AttunmentsNature.props` (gitignored) with your install path:

```xml
<Project>
  <PropertyGroup>
    <HadeanTacticsDir>C:\Program Files (x86)\Steam\steamapps\common\Hadean Tactics</HadeanTacticsDir>
  </PropertyGroup>
</Project>
```

2. Build:

```powershell
dotnet restore
dotnet build -c Release
```

DLL copies to `BepInEx/plugins/AttunmentsNature`. A zip is also written to `bin/Publish/AttunmentsNature.zip`.

## Config

`BepInEx/config/AttunmentsNature.cfg`

Edit while the game is closed. BepInEx Configuration Manager does not work in Hadean Tactics.

### `[Attunment]`

| Setting | Default | Meaning |
|---------|---------|---------|
| Debug | false | Extra log lines |
| Buff Value | 50 | Stacks (or forstbite seconds) for the test card |
| Element | burn | Fixed element for the test card: `burn`, `decay`, `forstbite`, or `shock` |

In-game config UI (if available): **Add buff card to hand** draws an ally-target attunment card using those settings. Play it on a unit, then attack.

### `[Hero Unit]`

| Setting | Default | Meaning |
|---------|---------|---------|
| Debug | false | Extra log lines |
| Visual Donor Id | moonhunter | Unit id used for the hero model |
| Skill Element | burn | Element applied to self on skill cast |
| Skill Value | 10 | Buff value for the hero skill |

**Add to bench** registers/spawns `my_hero` with skill `skill_my_hero` (`TargetType.Source` → self-buff). Re-click after changing skill settings so the unit is re-registered.

## Project layout

| File | Purpose |
|------|---------|
| `AttunmentsNature.cs` | Plugin entry |
| `AttunmentEffect.cs` | Buff helpers, test card, Harmony patches |
| `hero.cs` | Hero unit + self-attunment skill |
| `AttunmentsNature.props` | Local game path (not committed) |

## Notes

- Valid elements are only `burn`, `decay`, `forstbite`, `shock` (not `fire` / `poison` / `ice`).
- Cards must use `IsMod = false` unless you ship a game ModContainer with VFX for the card id.
- Hero unit skills that buff the caster should use `TargetType.Source`, not `AllyOnly` / `RandomEmptyTile`.

## SDK

References [NuggetTactics.SDK](https://www.nuget.org/packages/NuggetTactics.SDK) **1.0.2**.

See the [NuggetTactics.SDK README](https://github.com/AZander48/NuggetTactics.SDK) for details.
