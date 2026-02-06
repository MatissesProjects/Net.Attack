# NodeExpansionPack

A collection of high-powered custom nodes for the coding terminal.

## Included Nodes

1.  **MEGA PROCESSOR**: An advanced processor node with **0 delay**, enabling near-instant execution.
2.  **INSTA-KILL PROCESSOR**: A specialized damage node pre-configured to deal 999,999 damage.
3.  **TURBO TRIGGER**: A rapid-fire attack trigger that fires at 5x normal speed.
4.  **ENDLESS LOOP**: A for-loop node pre-configured for 1,000 iterations.

## Features

- **Shop Injection**: Uses `AddNodeShopHijack` to force the MEGA PROCESSOR to appear in the terminal shop.
- **Advanced Templates**: Uses `CreateTemplate<T>` and `SetActionField` from `ModUtils` to clone existing nodes and surgically modify their internal logic logic (cooldowns, damage values, iteration counts) without breaking the game's data structure.