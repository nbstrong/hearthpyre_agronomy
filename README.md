# Hearthpyre Agronomy

Hearthpyre Agronomy is an extension for the Caves of Qud mod **Hearthpyre**. It adds a dedicated **Agronomy** category to the xyloschemer and lets you build Harvestry plants as settlement infrastructure.

## What It Adds

- A new xyloschemer category named `Agronomy`
- Harvestry plants from the Caves of Qud wiki, excluding:
  - mushroom case
  - gnawed watervine
  - phase web
  - shattered mirror
  - vantabloom
  - elcatl
  - witchwood wreath
- A build requirement for each plant:
  - the plant blueprint is placed normally
  - building the plant requires one matching `HarvestInto` item in your inventory
  - one `HarvestInto` item is consumed when the plant is built
- Growth timing based on the plant's `H Value`
  - growth time is measured in days
  - the minimum growth time is 1 day

## Requirements

- Caves of Qud
- Hearthpyre
- A mod loader setup that can load both mods together

## Installation

1. Put the `hearthpyre_agronomy` folder in your Caves of Qud `Mods` directory.
2. Make sure `Hearthpyre` is installed and enabled.
3. Enable `HearthpyreAgronomy`.
4. Launch the game and open the xyloschemer scheme menu.
