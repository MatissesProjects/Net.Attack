# VortexBladeMod

Introduces the **VORTEX BLADE** upgrade, adding defensive spectral blades that orbit the player.

## Features

- **Orbital Defense**: Spectral blades rotate around the player, slicing through nearby enemies.
- **Dynamic Visuals**: 
  - Pulsating line renderers for the blades.
  - Glowing circular particle trails (dynamically replaced textures).
- **Scaling Mechanics**:
  - **Damage**: Scales with both upgrade stacks and player level.
  - **Attack Speed**: Damage interval starts at 250ms and decreases by 15% (compounding) per stack.
  - **Crowd Control**: Unlocks knockback capabilities at level 3 and above.

## Technical Details

- **Base Library**: Built on `NetAttackModUtils`.
- **Shop Integration**: Uses `RegisterModdedUpgrade` to seamlessly integrate into the shop rotation alongside other mods.
- **Resource Swapping**: Automatically scans game assets to replace square particle masks with circular ones for a cleaner look.