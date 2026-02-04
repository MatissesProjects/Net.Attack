namespace TestLibrary;
using BepInEx;
using HarmonyLib;

[BepInPlugin("com.YourName.NetAttackMod", "My First Mod", "1.0.0")]
public class NetAttackMod : BaseUnityPlugin
{
    void Awake()
    {
        // Apply all patches when the mod loads
        Harmony.CreateAndPatchAll(typeof(NetAttackMod));
        Logger.LogInfo("Net.Attack() Mod Loaded!");
    }

    // This is a "Hook". It targets the Class "Wallet" and method "AddMoney"
    // (You must verify these names in dnSpy first!)
    [HarmonyPatch(typeof(BRG.Gameplay.Units.HealthComponent), "TakeDamage")]
    [HarmonyPrefix] // Run this BEFORE the original code
    static void Prefix(ref float damage)
    {
        damage = 420f;
        // BRG.Gameplay.Units.Player().SetGodMode();
    }
}