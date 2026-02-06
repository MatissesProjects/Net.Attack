# SizeIncreaseMod

A standalone mod that introduces the **GIANT GROWTH** upgrade to Net.Attack.

## Features

- **Physical Growth**: Increases player size by 50% per stack.
- **Visual Feedback**: Applies a green color-pulsing effect to the player sprite to indicate growth.
- **Balanced Integration**: Competes fairly with other mods for shop slots using the centralized `NetAttackModUtils` injection system.

## Technical Details

- **Base Library**: Built on `NetAttackModUtils`.
- **Injection**: Uses `RegisterModdedUpgrade` for randomized, conflict-free shop appearance.
- **Tracking**: Uses `AddUpgradeSelectionTracker` to count stacks regardless of which shop slot the upgrade appears in.
- **Scaling**: Modifies the `transform.localScale` of the player object.
