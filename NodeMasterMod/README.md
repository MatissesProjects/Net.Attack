# NodeMasterMod

A global balance-breaking mod that overclocks all standard nodes in the game.

## Features

- **Economy Hack**: Sets the price of all nodes to 1.
- **Unlimited Scalability**: Removes placement limits (Max Count set to 99) for all nodes.
- **Global Overclocking**: Automatically detects and reduces delays/cooldowns on all node actions by 80%.

## Technical Details

- **Base Library**: Built on `NetAttackModUtils`.
- **Dynamic Scanning**: Uses `FindAndModifyNodes` to iterate over every `NodeSO` in the game's database.
- **Logic Refactoring**: Uses `OverclockNodeAction` to reflectively identify and reduce "delay" or "cooldown" fields on any action type, making it compatible with future game updates or other mods.