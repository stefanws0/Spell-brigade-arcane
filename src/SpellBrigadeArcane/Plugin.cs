namespace SpellBrigadeArcane;

using BepInEx;
using BepInEx.Unity.IL2CPP;

/// <summary>BepInEx entry point: sets up logging and config and injects <see cref="ArcaneRunner"/>.</summary>
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "com.stefanws0.spellbrigadearcane";
    public const string Name = "SpellBrigadeArcane";
    public const string Version = "0.2.0";

    public override void Load()
    {
        ModLog.Init(Log);
        ModLog.Step("Load: config");
        ModConfig.Init(Config);
        ModLog.Step("Load: registering ArcaneRunner");
        AddComponent<ArcaneRunner>();
        ModLog.Step("Load: done");
        ModLog.Info($"{Name} {Version} loaded. Scan: {(ModConfig.Scan.Value ? "ON" : "off")}.");
    }
}
