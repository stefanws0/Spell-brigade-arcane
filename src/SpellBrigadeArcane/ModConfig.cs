namespace SpellBrigadeArcane;

using BepInEx.Configuration;

/// <summary>The mod's entries in BepInEx/config/com.stefanws0.spellbrigadearcane.cfg.</summary>
internal static class ModConfig
{
    public static ConfigEntry<bool> Scan { get; private set; } = null!;

    public static void Init(ConfigFile config)
    {
        Scan = config.Bind("Debug", "Scan", true,
            "Write BepInEx/SpellBrigadeArcane-scan.txt (the previous one is kept as ...-scan.prev.txt): the Enchantments screen " +
            "(scanned up to 3 times per session, each time it opens), the enchantments you hover, and your wizard's stats " +
            "(once per mission, max 5). Read-only development aid.");
    }
}
