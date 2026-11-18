namespace SpellBrigadeArcane;

using BepInEx.Configuration;

/// <summary>The mod's entries in BepInEx/config/com.stefanws0.spellbrigadearcane.cfg.</summary>
internal static class ModConfig
{
    public static ConfigEntry<bool> Scan { get; private set; } = null!;

    public static void Init(ConfigFile config)
    {
        Scan = config.Bind("Debug", "Scan", true,
            "Write BepInEx/SpellBrigadeArcane-scan.txt: the Enchantments screen (the first time it is open in a session) " +
            "and your wizard's stats (once per mission). Read-only development aid.");
    }
}
