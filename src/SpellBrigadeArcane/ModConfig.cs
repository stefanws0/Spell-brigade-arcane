namespace SpellBrigadeArcane;

using BepInEx.Configuration;

/// <summary>The mod's entries in BepInEx/config/com.stefanws0.spellbrigadearcane.cfg.</summary>
internal static class ModConfig
{
    public static ConfigEntry<bool> Scan { get; private set; } = null!;

    public static ConfigEntry<bool> UnlockRow { get; private set; } = null!;

    public static ConfigEntry<bool> DumpArt { get; private set; } = null!;

    public static ConfigEntry<string> TestLevelOverride { get; private set; } = null!;

    public static void Init(ConfigFile config)
    {
        Scan = config.Bind("Debug", "Scan", true,
            "Write BepInEx/SpellBrigadeArcane-scan.txt (the previous one is kept as ...-scan.prev.txt): the Enchantments screen " +
            "(scanned up to 3 times per session, each time it opens), the enchantments you hover, and your wizard's stats " +
            "(once per mission, max 5). Read-only development aid.");

        UnlockRow = config.Bind("Debug", "UnlockRow", false,
            "Open the sixth tier without the 12 Masteries it normally needs (for testing). Buying spends real gold; " +
            "middle-click a sixth-tier enchantment (or use the game's Refund All) to get it back.");

        DumpArt = config.Bind("Debug", "DumpArt", false,
            "Write the game's enchantment icons and how the Enchantments screen draws them to BepInEx/SpellBrigadeArcane-art/ " +
            "(each time the screen has been open 1.5 s, max 3 times per session). Read-only development aid.");

        TestLevelOverride = config.Bind("Debug", "TestLevels", "",
            "Test aid: missions use these levels instead of the ones you bought, e.g. \"Volley=6, Alchemy=6\" (6 = with " +
            "its Mastery). Costs no gold and changes no save. Leave empty to play normally.");
    }
}
