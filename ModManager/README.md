# Mod Manager

A BepInEx mod for **Net.Attack** that provides a management interface within the game's main menu to enable or disable mods.

## Features

- **In-Game Toggle**: Enables or disables installed mods without manually moving files.
- **Reboot Integration**: Notifies the user when a game restart is required to apply changes.
- **Dependency Management**: Ensures core utilities like `NetAttackModUtils` remain active.

## Installation

1.  Ensure you have **BepInEx** installed for Net.Attack.
2.  Build the project using `dotnet build`.
3.  Deploy `ModManager.dll` to your `BepInEx/plugins` folder using the deployment script in the project root.
4.  Launch the game and access the menu.