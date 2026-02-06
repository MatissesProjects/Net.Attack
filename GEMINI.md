## Gemini Added Memories
- When asked to deploy or sync mods, iterate through each project folder, locate the DLL in `bin\Debug\netstandard2.1\`, and copy it to `C:\Program Files (x86)\Steam\steamapps\common\ForeachHack\NetAttack\BepInEx\plugins` ONLY if the file already exists in the destination (performing a replacement).
- ALWAYS build the mod to see if there are errors before deployment.
- When starting a new project go through multiple planning steps, and create a plan document for future following.
- After big changes attempt to break into good chunks for git commits.

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
Safely attempts to patch a method using Harmony.
```csharp
ModUtils.PatchSafe(harmony, Log, "ClassName", "MethodName", typeof(PatchType));
```

#### `RegisterModdedUpgrade`
Registers an upgrade with the centralized injection system. The system handles randomization and duplicate prevention automatically.
```csharp
ModUtils.RegisterModdedUpgrade(harmony, () => myTemplate, () => shouldInject);
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
