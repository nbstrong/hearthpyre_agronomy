# Hearthpyre Agronomy

Hearthpyre Agronomy is a finished extension mod for Caves of Qud that adds a dedicated `Agronomy` category to the Hearthpyre xyloschemer. It lets you place Harvestry plants as settlement infrastructure and turns each plant into a repeatable, inventory-gated build option.

## What It Adds

- A new xyloschemer category named `Agronomy`
- Harvestry plants from the Caves of Qud wiki, with unsupported edge cases omitted
- A build requirement for each plant:
  - the blueprint is placed normally
  - building the plant requires one matching `HarvestInto` item in your inventory
  - the required item is consumed only after the plant object is successfully created
  - the normal tier-based xyloschemer charge cost is still paid for the build
- Persistent regrowth based on absolute game time
  - newly built plants start unripe
  - the first harvestable state arrives after the plant's `H Value`, with a minimum of 1 day
  - every successful harvest starts the same growth timer again
  - zones can be left, suspended, thawed, and reloaded without losing the deadline
- Startup validation for blueprint and ingredient IDs
- Guardrails for duplicate and normalized catalog collisions

## Requirements

- Caves of Qud
- Hearthpyre `2.2.3` or newer
- A mod loader setup that can load both mods together

## Installation

1. Put the `hearthpyre_agronomy` folder in your Caves of Qud `Mods` directory.
2. Make sure `Hearthpyre` is installed and enabled.
3. Enable `HearthpyreAgronomy`.
4. Launch the game and open the xyloschemer scheme menu.

## Project Layout

- `Mod/manifest.json` declares the mod metadata and the Hearthpyre dependency.
- `Mod/Hearthpyre.json` defines the Agronomy catalog data.
- `Mod/CS/AgronomyBootstrap.cs` hooks the Hearthpyre blueprint build path and validates the catalog.
- `Mod/CS/AgronomyGrowth.cs` stores regrowth deadlines and reconciles them against global game time.

## Notes

- This repository is an extension of Hearthpyre only. The `hearthpyre/` folder is kept untouched.
- The cultivated plants keep their native behavior unless the agronomy logic needs to override it for persistence.
