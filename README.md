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
- Guardrails for duplicate blueprint names in the Agronomy catalog

## Requirements

- Caves of Qud: the exact game build has not yet been verified in-game for this unreleased version
- Hearthpyre `2.2.3` exactly
- A mod loader setup that can load both mods together

The dependency is intentionally capped at Hearthpyre `2.2.3`, the release whose
`HearthpyreBlueprint.Build(GameObject, bool)` and `Harvestable.UpdateRipeStatus(bool)`
signatures this extension patches. Test a newer Hearthpyre release before widening the
manifest range.

## Installation

1. Put the `hearthpyre_agronomy` folder in your Caves of Qud `Mods` directory.
2. Make sure `Hearthpyre` is installed and enabled.
3. Enable `HearthpyreAgronomy`.
4. Launch the game and open the xyloschemer scheme menu.

## Project Layout

- `manifest.json` declares the mod metadata and the Hearthpyre dependency.
- `Hearthpyre.json` defines the Agronomy catalog data.
- `CS/AgronomyBootstrap.cs` hooks the Hearthpyre blueprint build path and validates the catalog.
- `CS/AgronomyGrowth.cs` stores regrowth deadlines and reconciles them against global game time.

## Package and Validate

The release package is source-only. Include `manifest.json`, `Hearthpyre.json`, `CS/`,
`README.md`, `CHANGELOG.md`, and `LICENSE`; exclude `bin/`, `obj/`, and any separately
compiled Agronomy DLL.

Install Hearthpyre `2.2.3` and this package in Caves of Qud, then let the game compile the
mods. Before publishing, verify the Qud mod build log contains no Agronomy compiler errors and
complete the in-game checks below. Record the exact Caves of Qud build with that verification.

## Notes

- This repository is an extension of Hearthpyre only. The `hearthpyre/` folder is kept untouched.
- The cultivated plants keep their native behavior unless the agronomy logic needs to override it for persistence.
