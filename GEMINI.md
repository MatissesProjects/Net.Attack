This is a set of projects to make a mod for the game Net.Attack

We have access to the file AllClasses.txt which should be brought into context when needed
    we might need this in the case that we need a function or class from dnspy

we are using dnspy to gather as much information as we can for the game

if we need to use dnspy for something specific let me know

ALWAYS build the mod to see if there are errors

---

## Deployment (Syncing Mods)

To automatically copy built DLLs to the game's plugin folder (only updating existing ones), use this PowerShell script:

```powershell
$dest = 'C:\Program Files (x86)\Steam\steamapps\common\ForeachHack\NetAttack\BepInEx\plugins';
$mods = Get-ChildItem -Directory;

foreach ($mod in $mods) {
    $dllName = "$($mod.Name).dll";
    $srcPath = Join-Path $mod.FullName "bin\Debug\netstandard2.1\$dllName";
    $destPath = Join-Path $dest $dllName;

    if (Test-Path $srcPath) {
        if (Test-Path $destPath) {
            Copy-Item -Path $srcPath -Destination $destPath -Force;
            Write-Host "Updated: $dllName";
        } else {
            Write-Host "Skipped (Not present in destination): $dllName";
        }
    }
}
```

---

## Shared Utilities (`NetAttackModUtils`)

The `NetAttackModUtils` library provides a shared foundation for mods, encapsulating complex reflection and patching logic to keep individual mod files clean.

<details>
<summary><strong>How to use ModUtils</strong></summary>

### 1. Project Configuration
Ensure your mod project references `NetAttackModUtils.csproj`.

### 2. Core Methods

#### `PatchSafe`
Safely attempts to patch a method using Harmony. It handles method lookup and logging internally to prevent the entire plugin from failing if a single patch target changes in a game update.
```csharp
ModUtils.PatchSafe(harmony, Log, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijackPatch));
```

#### `ApplyMetadata`
Initializes a `ScriptableObject` (usually an instantiated template) with custom ID, Name, and Description keys. It also resets common level/price fields and prepares internal wrappers.
```csharp
ModUtils.ApplyMetadata(myTemplate, "MY_UNIQUE_ID", "MY_NAME_KEY", "MY_DESC_KEY");
```

#### `CreateTemplate<T>`
Clones an existing `ScriptableObject` from a list by its ID and applies new metadata in one step.
```csharp
var newNode = ModUtils.CreateTemplate<ScriptableObject>(list, "TemplateID", "NewID", "Name", "Desc");
```

#### `SetActionField`
Specific helper for `NodeSO` objects to find and set a field on the internal `ActionBase` logic object.
```csharp
ModUtils.SetActionField(node, "cooldown", 0.05f);
```

#### `IsShopOpen`
Returns `true` if the upgrade shop is currently open.
```csharp
if (ModUtils.IsShopOpen()) { ... }
```

#### `AddUpgradeShopHijack`
Forces a specific upgrade into a shop slot.
```csharp
ModUtils.AddUpgradeShopHijack(harmony, slotIndex, () => myTemplate, () => shouldInject);
```

#### `AddUpgradeSelectionTracker`
Tracks when an upgrade is selected in the shop.
```csharp
ModUtils.AddUpgradeSelectionTracker(harmony, (index, upgrade) => { ... });
```

#### `IsCodingScreenActive`
Heuristic detection for whether the user is in a coding/terminal screen versus active gameplay. Maintains state based on movement.
```csharp
status = ModUtils.IsCodingScreenActive(status, ref holdTimer);
```

---

## Getting Started (Example Mod)

We have provided a fully functional `ExampleMod` to help you get started.

<details>
<summary><strong>How to create a new mod</strong></summary>

### 1. Copy the Example
Copy the `ExampleMod` folder and rename it to your mod's name (e.g., `MyCoolMod`).

### 2. Rename the Project
Rename `ExampleMod.csproj` to `MyCoolMod.csproj` and open it. Update the `<AssemblyName>` and `<Description>` tags.

### 3. Update the Code
Open `NetAttackMod.cs`:
1.  Change the `namespace` to `MyCoolMod`.
2.  Update the `[BepInPlugin]` attribute with your unique GUID (e.g., `com.Me.MyCoolMod`).
3.  Use `ModUtils.PatchSafe` to hook into game methods.

### 4. Build
Run `dotnet build` in your mod's folder. The DLL will be in `bin/Debug/netstandard2.1/`.

</details>

<details>
<summary><strong>Reference: ExampleMod Code</strong></summary>

The `ExampleMod` demonstrates how to create a simple "God Mode" by patching the health component:

```csharp
// Use ModUtils for safe patching
ModUtils.PatchSafe(harmony, Log, 
    "BRG.Gameplay.Units.HealthComponent", 
    "TakeDamage", 
    typeof(GodModePatch)
);

// The patch itself
public static class GodModePatch
{
    public static bool Prefix(ref float amount)
    {
        amount = 0f; // Negate all damage
        return true; // Continue execution
    }
}
```
</details>

#### `GetPlayerLevel`
Safely retrieves the current player's level via reflection.
```csharp
int level = ModUtils.GetPlayerLevel(this);
```

<details>
<summary><i>Advanced: Reflection Helpers</i></summary>

#### `SetField`
A recursive reflection helper that finds and sets fields or properties by name, searching up the class hierarchy.
```csharp
ModUtils.SetField(obj, typeof(MyType), "_internalField", value);
```

#### `ModifyWrapper`
Specifically targets internal "wrapper" objects (like `gameplayUpgrade` or `nodeUpgrade`) often found inside game data structures, clearing their attributes and setting description keys.

</details>

</details>