# ExpeditionRunePreview

An [ExileCore2](https://github.com/exCore2/ExileCore2) (Path of Exile 2) plugin that previews Expedition
remnant encounters before you reach them, so you can decide whether a remnant is worth the detour.

## What it shows

- **Rune count** of each remnant in the zone, read from the `Expedition2Encounter` entity's state
  machine (`sockets`). This is available the moment the area loads, from anywhere on the map.
- **Which runes** the area has, detected from its preloaded rune `.pet` files.
- **Craftable rewards** for the detected runes — every recipe whose rune requirement is no greater than
  the remnant's socket count — sorted by value.
- **Reward values** in Exalted Orbs, read from the cached poe.ninja data downloaded by the
  [NinjaPricer](https://github.com/exCore2/NinjaPricer) plugin (optional; values are simply omitted if
  NinjaPricer isn't installed or hasn't fetched prices).

## Requirements

- ExileCore2
- (Optional) NinjaPricer, for reward valuations. ExpeditionRunePreview reads NinjaPricer's on-disk price
  cache directly — it does not modify or depend on the NinjaPricer assembly.

## Building

The project references `ExileCore2.dll` via the `ExileCore2Package` MSBuild property and outputs to
`ExApiPluginOutputPath`, matching the standard ExileCore2 plugin build setup. Drop it in
`Plugins/Source/ExpeditionRunePreview` and let the loader compile it, or build with `dotnet build`.

## Settings

- Filter rewards by socket count (on by default)
- Show reward values (NinjaPricer prices)
- Show/hide unknown runes, recipe cap per rune, colors, panel offset
- Refresh-preloads hotkey (default `F5`)

## Rune recipe data

Recipe definitions live in [`Data/recipes.json`](Data/recipes.json) — reward, the runes it needs, and
display names/aliases. Reload it in-game with the **Reload rune data** button in settings after editing.
