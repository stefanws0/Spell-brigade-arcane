namespace SpellBrigadeArcane;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

/// <summary>BepInEx entry point: sets up logging and config and injects <see cref="ArcaneRunner"/>.</summary>
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "com.stefanws0.spellbrigadearcane";
    public const string Name = "SpellBrigadeArcane";
    public const string Version = "0.5.1";

    /// <summary>For the mod's one patch (Long Shot). Nothing is patched at load.</summary>
    public static Harmony Harmony { get; private set; } = null!;

    public override void Load()
    {
        ModLog.Init(Log);
        Harmony = new Harmony(Guid);
        ModLog.Step("Load: config");
        ModConfig.Init(Config);
        ModLog.Step("Load: registering ArcaneRunner");
        AddComponent<ArcaneRunner>();
        ModLog.Step("Load: done");
        ModLog.Info($"{Name} {Version} loaded. Scan: {(ModConfig.Scan.Value ? "ON" : "off")}.");
    }
}
