# Net.Attack Mod Collection

A comprehensive set of modular mods for Net.Attack, built on a robust shared utility framework.

## Architecture

This project is designed with modularity and stability in mind. All mods depend on **[NetAttackModUtils](./NetAttackModUtils)**, which provides:
- **Centralized Injection**: A conflict-free system for injecting items into the Upgrade Shop and Node Terminal.
- **Safe Patching**: Wrappers to prevent crashes during game updates.
- **Standardized Helpers**: Unified methods for creating templates, modifying logic, and detecting game state.

## Modules

### Upgrades (The Shop)
- **[HyperSpeedMod](./HyperSpeedMod)**: Insane movement speed and character growth.
- **[VortexBladeMod](./VortexBladeMod)**: Orbital spectral blades with scaling damage and speed.
- **[SingularityMod](./SingularityMod)**: Powerful gravitational vortex ability with custom UI and energy mechanics.

*Note: These mods use a randomized injection system. They will compete for up to 2 slots in the shop to ensure a balanced experience.*

### Nodes (The Terminal)
- **[NodeExpansionPack](./NodeExpansionPack)**: Adds new high-powered nodes like the MEGA PROCESSOR and INSTA-KILL.
- **[NodeMasterMod](./NodeMasterMod)**: A global overclock mod that makes all standard nodes cheaper and faster.

## Build Instructions
1. Open the solution in your preferred IDE.
2. Ensure you have the game dependencies (Assembly-CSharp.dll, etc.) referenced correctly.
3. Build the solution. All mods will output to their respective `bin/Debug/netstandard2.1/` folders.
