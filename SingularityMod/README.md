# SingularityMod

Adds the **Singularity Vortex** ability, allowing players to pull enemies into a gravity well and release them in a destructive blast.

## Features

- **Gravitational Pull**: Hold `G` to pull nearby enemies into orbit.
- **Energy System**: Uses a custom energy bar with recharge and overheat mechanics.
- **Discharge Blast**: Release `G` to push enemies back and deal massive damage.
- **Level Scaling**: Radius and damage increase with player level and upgrade stacks.
- **Dynamic UI**: Custom On-Screen Display (OSD) for energy tracking.

## Technical Details

- **Base Library**: Built on `NetAttackModUtils`.
- **Heuristics**: Uses `IsCodingScreenActive` to automatically hide the UI and disable logic when entering terminals.
- **Injection**: Uses `RegisterModdedUpgrade` to appear in the shop. The upgrade increases the Singularity Level, boosting radius and damage.
