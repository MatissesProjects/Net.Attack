This is a set of projects to make a mod for the game Net.Attack

We have access to the file AllClasses.txt which should be brought into context when needed
    we might need this in the case that we need a function or class from dnspy

we are using dnspy to gather as much information as we can for the game

if we need to use dnspy for something specific let me know

ALWAYS build the mod to see if there are errors

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

#### `IsShopOpen`
Returns `true` if the upgrade shop is currently open.
```csharp
if (ModUtils.IsShopOpen()) { ... }
```

#### `IsCodingScreenActive`
Heuristic detection for whether the user is in a coding/terminal screen versus active gameplay. Maintains state based on movement.
```csharp
status = ModUtils.IsCodingScreenActive(status, ref holdTimer);
```

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
