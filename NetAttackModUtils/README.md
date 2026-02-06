# NetAttackModUtils

The comprehensive shared utility library for Net.Attack mods. This project centralizes common reflection, patching, and game state detection logic to provide a robust foundation for all modding activities.

## Core Features

### 🛡️ Safe Patching
- `PatchSafe`: Wrapper for Harmony patching that prevents entire plugin failures if a single method signature changes.

### 💉 Injection Systems
- **Smart Upgrade Shop Injection**: `RegisterModdedUpgrade` allows mods to register their upgrades. The system automatically:
  - Randomly selects up to 2 modded upgrades per shop visit.
  - Injects them into random slots.
  - Prevents duplicates (no two identical upgrades in one shop).
- **Node Terminal Injection**: `AddNodeShopHijack` forces custom nodes to appear in the coding terminal's shop.

### 🧩 Template & Metadata
- `CreateTemplate<T>`: Streamlines cloning existing game objects (Nodes/Upgrades) and applying new IDs/Names.
- `ApplyMetadata`: Handles the low-level reflection needed to set internal ID, Name, Description, and Price fields.
- `SetActionField` & `OverclockNodeAction`: Helpers for modifying the logic within `NodeSO` objects (e.g., reducing cooldowns, changing damage).

### 🔍 Game State Detection
- `IsShopOpen()`: Reliable detection of the upgrade shop UI state.
- `IsCodingScreenActive()`: Heuristic movement-based detection for terminal/coding screens.
- `GetPlayerLevel()`: Reflection-based retrieval of the current player's level.

### 🎮 Event Tracking
- `AddUpgradeSelectionTracker`: A multicast delegate system that lets multiple mods track when their specific upgrades are purchased, regardless of which slot they appeared in.