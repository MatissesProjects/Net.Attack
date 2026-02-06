## Gemini Added Memories
- **Centralized Architecture**: Always prefer moving logic to `NetAttackModUtils` if it's reused across mods (e.g., patching, shop injection, state detection).
- **Deployment Strategy**: When deploying, check for existing DLLs in the game folder. Replace existing ones to update, but be cautious about adding new files unless explicitly intended. The standard script handles this logic.
- **Shop Hijacking**: Do not manually patch `UpgradeShop:SetupShop` in individual mods. Use `ModUtils.RegisterModdedUpgrade` to ensure fair slot distribution and prevent conflicts/crashes.
- **Node Injection**: Use `ModUtils.AddNodeShopHijack` to safely insert nodes into the terminal shop.
- **Templates**: Always use `ModUtils.CreateTemplate<T>` when making new ScriptableObjects to ensure they inherit valid internal state from the game.
- **Safety**: Always wrap patches in `try-catch` blocks or use `ModUtils.PatchSafe`.

---

This is a set of projects to make a mod for the game Net.Attack

We have access to the file AllClasses.txt which should be brought into context when needed
    we might need this in the case that we need a function or class from dnspy

we are using dnspy to gather as much information as we can for the game

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
            Copy-Item -Path $srcPath -Destination $destPath -Force;
            Write-Host "Deployed New: $dllName";
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
Safely attempts to patch a method using Harmony.
```csharp
ModUtils.PatchSafe(harmony, Log, "ClassName", "MethodName", typeof(PatchType));
```

#### `RegisterModdedUpgrade`
Registers an upgrade with the centralized injection system. The system handles randomization (max 2 slots) and duplicate prevention automatically.
```csharp
ModUtils.RegisterModdedUpgrade(harmony, () => myTemplate, () => shouldInject);
```

#### `AddUpgradeSelectionTracker`
Tracks when an upgrade is selected in the shop.
```csharp
ModUtils.AddUpgradeSelectionTracker(harmony, (index, upgrade) => { ... });
```

#### `AddNodeShopHijack`
Forces a custom node to appear in the terminal shop.
```csharp
ModUtils.AddNodeShopHijack(harmony, () => myTemplate, () => true);
```

#### `IsShopOpen` / `IsCodingScreenActive`
Helpers for detecting game state.
```csharp
if (ModUtils.IsShopOpen()) { ... }
status = ModUtils.IsCodingScreenActive(status, ref timer);
```

#### `GetPlayerLevel`
Safely retrieves the current player's level via reflection.
```csharp
int level = ModUtils.GetPlayerLevel(this);
```

<details>
<summary><i>Advanced: Reflection Helpers</i></summary>

#### `SetField`
A recursive reflection helper that finds and sets fields or properties by name.
```csharp
ModUtils.SetField(obj, typeof(MyType), "_internalField", value);
```

#### `SetActionField`
Specific helper for `NodeSO` objects to modify the internal `ActionBase` logic.
```csharp
ModUtils.SetActionField(node, "cooldown", 0.05f);
```

#### `CreateTemplate<T>`
Clones an existing `ScriptableObject` from a list by its ID and applies new metadata in one step.
```csharp
var newNode = ModUtils.CreateTemplate<ScriptableObject>(list, "TemplateID", "NewID", "Name", "Desc");
```

</details>

</details>

---

## Getting Started (Example Mod)

We have provided a fully functional `ExampleMod` to help you get started.

<details>
<summary><strong>How to create a new mod</strong></summary>

1. Copy `ExampleMod` folder.
2. Rename `.csproj` and update `<AssemblyName>`.
3. Update `namespace` and `[BepInPlugin]` GUID.
4. Use `ModUtils` for logic.
5. Build and Deploy.

</details>
