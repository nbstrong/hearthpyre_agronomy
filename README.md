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

- Caves of Qud
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

## Build and Package

Set `QudManaged` to the Caves of Qud managed-assembly directory, then run:

```bash
dotnet build HearthpyreAgronomy.csproj -p:QudManaged=/path/to/CavesOfQud/Qud_Data/Managed
```

For a release package, include `manifest.json`, `Hearthpyre.json`, `CS/`, the compiled
assembly, `README.md`, `CHANGELOG.md`, and `LICENSE`. Do not include `bin/` or `obj/`.

## Release Checks

- Build from a clean checkout with Hearthpyre `2.2.3`.
- Start a new game and load a save created before the release.
- Build with stacked and missing ingredients; confirm a failed post-build setup removes the
  constructed plant, retains the ingredient, and refunds available xyloschemer charge.
- Harvest and regrow a plant repeatedly; repeated unripe updates must not delay its deadline.
- Leave, reactivate, freeze, thaw, and reload a zone containing growing plants.
- Verify a blueprint appearing in another Hearthpyre category does not receive Agronomy behavior.
- Install alongside another Hearthpyre extension, then smoke-test the packaged mod.

## Notes

- This repository is an extension of Hearthpyre only. The `hearthpyre/` folder is kept untouched.
- The cultivated plants keep their native behavior unless the agronomy logic needs to override it for persistence.
