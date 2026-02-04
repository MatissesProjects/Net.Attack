# Singularity Vortex Mod for Net.Attack()

A BepInEx mod for **Net.Attack()** that adds a powerful, risk-reward "Singularity" ability to the player's arsenal.

## Overview

The **Singularity Vortex** allows you to manipulate the battlefield by generating a gravitational field that pulls enemies towards you. It effectively groups enemies for AOE attacks but requires careful energy management to avoid a catastrophic meltdown.

### Key Features

*   **Gravitational Pull:** Hold the activation key to pull nearby enemies into orbit around you.
*   **Energy System:** The ability drains energy over time. The more enemies you trap, the faster it drains.
*   **Vampiric Healing:** Killing enemies while the vortex is active consumes a small amount of energy to heal you.
*   **Discharge Blast:** Releasing the ability releases a shockwave, pushing enemies back and dealing damage.
*   **Meltdown Mechanic:** If energy hits 0%, the core destabilizes, dealing damage to you and entering an "Overheated" state where it cannot be used until fully recharged.
*   **Level Scaling:** The radius, pull force, and blast damage scale with your player level. -- Currently doesnt work, but will be added in once we can get this mechanic going

## Controls

*   **Hold `G`**: Activate Singularity Vortex (Pull enemies).
*   **Release `G`**: Deactivate and trigger Discharge Blast (Push/Damage enemies).

## HUD

A custom energy bar is displayed on the screen:
*   **Cyan:** Normal operation.
*   **Yellow:** Low energy.
*   **Red:** Overheated (recharging).

## Mechanics in Detail

| Mechanic | Description |
| :--- | :--- |
| **Drain** | Base drain + extra drain per enemy caught in the field. |
| **Recharge** | Energy recharges automatically when inactive. Recharge rate increases with player level. |
| **Safe Radius** | The vortex radius expands as you have more energy and contracts as you run low. |
| **Overheat** | If you hit 0% energy, you take self-damage and cannot use the ability until it reaches 100% again. |

## Installation

1.  Ensure you have **BepInEx** installed for Net.Attack().
2.  Download the latest release of this mod.
3.  Place the `MyNetAttackMod.dll` file into your `BepInEx/plugins` folder.
4.  Launch the game.

## Building from Source

To build this project, you need the game DLLs referenced in the `.csproj` file.

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
