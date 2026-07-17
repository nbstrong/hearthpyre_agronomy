# Changelog

## 1.0.0

- Added the Agronomy xyloschemer category and Harvestry cultivation catalog.
- Consumed one matching harvested item only after a plant is configured successfully.
- Added persistent regrowth using each plant's H Value, with a minimum of one day.
- Removed Mushroom Case while Agronomy build identification remains blueprint-based.
- Prevented repeated unripe updates from postponing an active growth deadline.
- Skipped malformed unrelated `Hearthpyre.json` files without disabling a valid Agronomy catalog.
- Restricted the Hearthpyre dependency to the tested `2.2.3` release.
