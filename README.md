# Net.Attack Mod Collection

A comprehensive set of modular mods for Net.Attack, built on a robust shared utility framework.

## Architecture

This project is designed with modularity and stability in mind. All mods depend on **[NetAttackModUtils](./NetAttackModUtils)**, which provides:
- **Centralized Injection**: A conflict-free system for injecting items into the Upgrade Shop and Node Terminal.
- **Safe Patching**: Wrappers to prevent crashes during game updates.
- **Standardized Helpers**: Unified methods for creating templates, modifying logic, and detecting game state.

## Core Modules

### 🛠️ Utilities & Templates
- **[NetAttackModUtils](./NetAttackModUtils)**: The engine driving all mods. Handles reflection, patching, and shop logic.
- **[ExampleMod](./ExampleMod)**: A fully commented template for creating new mods. Includes a "God Mode" example.
- **[ModManager](./ModManager)**: Utility project for managing mod state and interactions.

### ⚡ Upgrades (The Shop)
- **[HyperSpeedMod](./HyperSpeedMod)**: Insane movement speed and character growth.
- **[VortexBladeMod](./VortexBladeMod)**: Orbital spectral blades with scaling damage and speed.
- **[SingularityMod](./SingularityMod)**: Powerful gravitational vortex ability with custom UI and energy mechanics.

*Note: Upgrade mods use a randomized injection system. They compete for up to 2 slots in the shop per visit to maintain balance.*

### 💻 Nodes (The Terminal)
- **[NodeExpansionPack](./NodeExpansionPack)**: Adds new high-powered nodes like the MEGA PROCESSOR, INSTA-KILL, and TURBO TRIGGER.
- **[NodeMasterMod](./NodeMasterMod)**: A global balance-breaking mod that overclocks all standard nodes, making them cheaper and faster.

## Build & Deployment
1. **Open**: Load the `NetAttack.sln` in Visual Studio or VS Code.
2. **Build**: Build the entire solution or individual projects. Output DLLs are located in `bin/Debug/netstandard2.1/` within each mod folder.
3. **Deploy**: Use the deployment script found in `GEMINI.md` to synchronize built DLLs with your game's `BepInEx/plugins` folder.