namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;
using Object = UnityEngine.Object;

/// <summary>
/// The local wizard in a mission and the game calls the bonuses need, in one place (facts: HANDOFF "Milestone C research").
/// </summary>
internal static class PlayerAccess
{
    /// <summary>The local player's identity in a mission (a new one every level; the lobby has none), or null.</summary>
    public static GameplayPlayerIdentity? LocalIdentity()
    {
        var found = Object.FindObjectsOfType(Il2CppType.Of<GameplayPlayerIdentity>());
        if (found == null) return null;
        foreach (var obj in found)
        {
            var identity = obj?.TryCast<GameplayPlayerIdentity>();
            if (identity != null && identity.IsOwner) return identity;
        }

        return null;
    }

    /// <summary>
    /// True once the game has set the wizard up: a body, a filled stats list and a spell. The game adds its own
    /// enchantments in that same setup (ApplyInitialStats, right after the first spell).
    /// </summary>
    public static bool IsSetUp(GameplayPlayerIdentity identity)
    {
        var stats = identity.stats;
        if (identity.currentGameplayPlayer == null || stats == null) return false;
        var list = stats.characterStats;
        var spells = stats.GetEquippedSpells();
        return list != null && list.Count > 0 && spells != null && spells.Count > 0;
    }

    /// <summary>The game's "no Enchantments" curse is active; the game then skips its own enchantments.</summary>
    public static bool NoEnchantmentsCurse()
    {
        var curses = GameUpgrades.Player.GetActiveCurses();
        if (curses == null) return false;
        for (var i = 0; i < curses.Count; i++)
        {
            var curse = curses[i];
            if (curse != null && curse.CurseIdentifier == CurseIdentifier.NoEnchantments) return true;
        }

        return false;
    }

    public static bool TryStatType(string name, out StatType stat) => Enum.TryParse(name, false, out stat) && Enum.IsDefined(stat);

    /// <summary>The wizard's own <c>Stat</c> of this type, or null if the character doesn't have it.</summary>
    public static Stat? CharacterStat(PlayerStats stats, StatType type)
    {
        var list = stats.characterStats;
        if (list == null) return null;
        for (var i = 0; i < list.Count; i++)
        {
            var stat = list[i];
            if (stat != null && stat.StatType == type) return stat;
        }

        return null;
    }

    public static StatCalc Calc(Stat stat) => RunBonuses.ParseCalc(stat.StatCalculationType.ToString());

    /// <summary>
    /// Sends character modifiers through the game's own network message, exactly like the game's StatUpgrade does for its
    /// enchantments: every PC in the session (host included) adds them to this wizard. One message for all of them.
    /// </summary>
    public static void SendCharacterModifiers(IEnumerable<(StatType Stat, float Value)> modifiers)
    {
        ModLog.StepOnce("send-mods", "PlayerAccess: first ImprovementsManager.AddStartingStatModifiers");
        var manager = NetworkSingleton<ImprovementsManager>.Instance;
        if (manager == null) throw new InvalidOperationException("the game's ImprovementsManager is missing");
        var list = new Il2CppSystem.Collections.Generic.List<StartingStatModifier>();
        foreach (var (stat, value) in modifiers)
        {
            var modifier = new StartingStatModifier();
            modifier.StatType = stat;
            modifier.Value = value;
            list.Add(modifier);
        }

        if (list.Count > 0) manager.AddStartingStatModifiers(list);
    }

    /// <summary>
    /// Adds a bonus to one spell's own stat, on this PC only: the game has no network message for that which doesn't also
    /// raise the spell's level. Returns the modifier value added, or null if the spell has no usable stat of this type.
    /// </summary>
    public static float? AddSpellBonus(PlayerStats stats, SpellType spell, StatType type, StatBonus bonus)
    {
        ModLog.StepOnce("spell-mod", "PlayerAccess: first SpellAttributes.AddImprovement");
        var all = stats.attributeForSpells;
        if (all == null || !all.TryGetValue(spell, out var attributes) || attributes == null) return null;
        var stat = attributes.GetStat(type);
        if (stat == null) return null;
        var value = RunBonuses.ModifierValue(bonus, Calc(stat), stat.BaseValue);
        if (value == null) return null;
        var modifier = new StartingStatModifier();
        modifier.StatType = type;
        modifier.Value = value.Value;
        attributes.AddImprovement(modifier);
        return value;
    }

    /// <summary>A spell's current value of a stat, or NaN if the game can't give one (the spell lacks the stat).</summary>
    public static float SpellValue(PlayerStats stats, SpellType spell, StatType type)
    {
        try
        {
            return stats.GetSpellValue(spell, type);
        }
        catch
        {
            return float.NaN;
        }
    }

    /// <summary>A character stat's current value for diagnostics, or NaN if the game throws.</summary>
    public static float CharacterValue(PlayerStats stats, StatType type)
    {
        try
        {
            return stats.GetCharacterValue(type);
        }
        catch
        {
            return float.NaN;
        }
    }

    /// <summary>The wizard's health component while it has a body, else null.</summary>
    public static PlayerHealthContainer? Health(GameplayPlayerIdentity identity)
    {
        var body = identity.currentGameplayPlayer;
        if (body == null) return null;
        var player = body.GetComponent<GameplayPlayer>();
        return player == null ? null : player.HealthContainer;
    }
}
