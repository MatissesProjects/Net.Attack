# Example Mod

This is a template project designed to help you start modding Net.Attack.

## How to use

1.  **Copy this folder**: Duplicate the `ExampleMod` directory.
2.  **Rename**: Rename the folder and the `.csproj` file to your new mod name.
3.  **Edit**: Open `NetAttackMod.cs` and follow the comments.
4.  **Build**: Run `dotnet build` to create your mod DLL.

## Features

- Uses `NetAttackModUtils` for safe and easy patching.
- Includes a simple "God Mode" example that prevents damage using the `ModUtils.PatchSafe` utility.