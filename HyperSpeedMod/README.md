# HyperSpeedMod

A standalone mod that introduces the **HYPER SPEED V3** upgrade to Net.Attack.

## Features

- **Extreme Mobility**: Increases movement speed by +1000% per stack.
- **Physical Growth**: Causes the player character to grow in size with each stack.
- **Visual Feedback**: Applies a color-pulsing effect to the player sprite while active.
- **Balanced Integration**: Competes fairly with other mods for shop slots using the centralized `NetAttackModUtils` injection system.

## Technical Details

- **Base Library**: Built on `NetAttackModUtils`.
- **Injection**: Uses `RegisterModdedUpgrade` for randomized, conflict-free shop appearance.
- **Tracking**: Uses `AddUpgradeSelectionTracker` to count stacks regardless of which shop slot the upgrade appears in.
- **Scaling**: Uses reflection to modify internal `PlayerMovement` fields.