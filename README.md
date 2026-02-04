# Set of Mod for Net.Attack()

BepInEx mods for **Net.Attack()** that adds some interesting different possibilities in the game.

## Overview

These are just a few of the mods we have created so far for this game so we can keep track of what is possible from our side.

## Goal 

I want to learn how to mod, and where the limit is of me being able to use BepInEx vs needing to modify the actual dll.

Very interested in seeing what we can do and what we can hook into. If you want to help please feel free to suggest anything.

## Installation

1.  In the zip there will be all the needed files for **BepInEx**.
2.  Place the `MyNetAttackMod.dll` file into your `BepInEx/plugins` folder.
3.  If you dont have one copy the one from the zip
4.  Launch the game.

## Building from Source

To build these projects, you need the game DLLs referenced in the `.csproj` file.

1.  Clone this repository.
2.  Update the `<HintPath>` in `TestLibrary.csproj` to point to your local Net.Attack() game folder.
    *   *Default path assumed:* `C:\Program Files (x86)\Steam\steamapps\common\ForeachHack\NetAttack\...`
3.  Build using Visual Studio or the .NET CLI:
    ```bash
    dotnet build
    ```

## Dependencies

*   [BepInEx](https://github.com/BepInEx/BepInEx)
*   [Harmony](https://github.com/pardeike/Harmony)
