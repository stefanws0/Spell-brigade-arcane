namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using SpellBrigadeArcane.Core;

/// <summary>
/// Gives the local wizard the sixth tier's bonuses, once per mission (a new GameplayPlayerIdentity each level; revives keep
/// it). Character stats go through the game's own network message, like the game's enchantments; Speed and Range go on
/// each equipped spell (this PC only), also on spells added later; Prism tops up Elemental Damage when a new element
/// appears. Polls twice a second; no patches.
/// </summary>
internal static class MissionBonuses
{
    private const float Interval = 0.5f;
    private const float SettleSeconds = 1f;
    private const float VerifyDelay = 1.5f;

    private static readonly HashSet<SpellType> SpellsDone = new();
    private static readonly List<StatType> VerifyStats = new();
    private static float _next;
    private static GameplayPlayerIdentity? _identity;
    private static int _appliedId;
    private static float _readySince = -1f;
    private static float _verifyAt = -1f;
    private static IReadOnlyList<StatBonus> _spellBonuses = Array.Empty<StatBonus>();
    private static PrismTracker? _prism;

    /// <summary>The local wizard this mission's bonuses went to, or null (lobby, or not set up yet).</summary>
    public static GameplayPlayerIdentity? Identity =>
        _identity != null && _appliedId != 0 && _identity.GetInstanceID() == _appliedId ? _identity : null;

    /// <summary>Alchemy's Mastery is owned (or set by TestLevels) and not cursed off this mission.</summary>
    public static bool PotionBeltActive { get; private set; }

    public static void Tick(float now)
    {
        if (now < _next) return;
        _next = now + Interval;

        if (_identity == null)
        {
            ModLog.StepOnce("bonus-find", "MissionBonuses: first FindObjectsOfType");
            _identity = PlayerAccess.LocalIdentity();
            if (_identity == null)
            {
                Forget();
                return;
            }
        }

        var identity = _identity;
        if (identity.GetInstanceID() != _appliedId)
        {
            if (!PlayerAccess.IsSetUp(identity))
            {
                _readySince = -1f;
                return;
            }

            if (_readySince < 0f) _readySince = now;
            if (now - _readySince < SettleSeconds) return;
            if (!Begin(identity, now)) return;
        }

        if (_verifyAt >= 0f && now >= _verifyAt) Verify(identity);
        AddToNewSpells(identity);
        UpdatePrism(identity);
    }

    /// <summary>Works out and sends this mission's bonuses. False to try again next tick (our levels aren't loaded yet).</summary>
    private static bool Begin(GameplayPlayerIdentity identity, float now)
    {
        if (!ArcaneState.EnsureLoaded() || ArcaneState.Current == null) return false;
        Forget();
        _appliedId = identity.GetInstanceID(); // set before sending anything: never send twice in one mission
        ModLog.Step("MissionBonuses: giving the sixth tier's bonuses");

        if (PlayerAccess.NoEnchantmentsCurse())
        {
            ModLog.Info("MissionBonuses: the no-Enchantments curse is on, so like the game's enchantments the sixth tier gives nothing this mission.");
            return true;
        }

        var levelOf = LevelSource();
        var bonuses = RunBonuses.ForLevels(ArcaneState.Defs, levelOf);
        var stats = identity.stats;
        var modifiers = new List<(StatType, float)>();
        foreach (var bonus in bonuses.Where(b => b.Target == BonusTarget.Character))
        {
            if (!TryCharacterModifier(stats, bonus, out var type, out var value)) continue;
            modifiers.Add((type, value));
            VerifyStats.Add(type);
        }

        if (modifiers.Count > 0) PlayerAccess.SendCharacterModifiers(modifiers);
        _spellBonuses = bonuses.Where(b => b.Target == BonusTarget.EverySpell).ToList();
        _prism = ArcaneState.Def(ArcaneId.Attunement).HasMastery(levelOf(ArcaneId.Attunement)) ? new PrismTracker() : null;
        PotionBeltActive = ArcaneState.Def(ArcaneId.Alchemy).HasMastery(levelOf(ArcaneId.Alchemy));
        _verifyAt = modifiers.Count > 0 ? now + VerifyDelay : -1f;
        ModLog.Info($"MissionBonuses: levels {Describe(levelOf)}; {modifiers.Count} character bonus(es) sent, " +
                    $"{_spellBonuses.Count} per-spell bonus(es), Prism {(_prism != null ? "on" : "off")}, Potion Belt {(PotionBeltActive ? "on" : "off")}.");
        return true;
    }

    /// <summary>The levels this mission uses: [Debug] TestLevels when set, else the bought ones.</summary>
    private static Func<ArcaneId, int> LevelSource()
    {
        var text = ModConfig.TestLevelOverride.Value;
        var test = TestLevels.Parse(text, Catalog.DefaultMaxLevel, out var problems);
        foreach (var problem in problems) ModLog.Warn("MissionBonuses: [Debug] TestLevels " + problem);
        if (test.Count > 0)
        {
            ModLog.Info($"MissionBonuses: using [Debug] TestLevels '{text}' instead of the bought levels (test aid).");
            return id => test.TryGetValue(id, out var level) ? level : 0;
        }

        return ArcaneState.Current!.Level;
    }

    private static string Describe(Func<ArcaneId, int> levelOf) =>
        string.Join(", ", Enum.GetValues<ArcaneId>().Select(id => $"{id} {levelOf(id)}"));

    /// <summary>The modifier for a character bonus, checked against the wizard's own stat. False (and a warning) if it can't be given.</summary>
    private static bool TryCharacterModifier(PlayerStats stats, StatBonus bonus, out StatType type, out float value)
    {
        value = 0f;
        if (!PlayerAccess.TryStatType(bonus.Stat, out type))
        {
            ModLog.Warn($"MissionBonuses: the game has no stat '{bonus.Stat}' ({bonus.Source}); skipped.");
            return false;
        }

        var stat = PlayerAccess.CharacterStat(stats, type);
        if (stat == null)
        {
            ModLog.Warn($"MissionBonuses: {bonus.Stat} is not a character stat ({bonus.Source}); skipped.");
            return false;
        }

        var calc = PlayerAccess.Calc(stat);
        var result = RunBonuses.ModifierValue(bonus, calc, stat.BaseValue);
        if (result == null)
        {
            ModLog.Warn(FormattableString.Invariant($"MissionBonuses: can't add {bonus.Amount:0.##}{(bonus.IsPercent ? "%" : "")} to {bonus.Stat} ({calc}, base {stat.BaseValue:0.###}); skipped."));
            return false;
        }

        value = result.Value;
        ModLog.Info(FormattableString.Invariant($"MissionBonuses: {bonus.Source}: {bonus.Stat} +{value:0.###} ({calc}); before: {stats.GetCharacterValue(type):0.###}"));
        return true;
    }

    /// <summary>Once, after the game's message had time to arrive: the character values now (they should have risen).</summary>
    private static void Verify(GameplayPlayerIdentity identity)
    {
        _verifyAt = -1f;
        var stats = identity.stats;
        foreach (var type in VerifyStats.Distinct())
            ModLog.Info(FormattableString.Invariant($"MissionBonuses: after the game's message: {type} = {stats.GetCharacterValue(type):0.###}"));
    }

    /// <summary>Speed and Range on every equipped spell that hasn't had them yet this mission.</summary>
    private static void AddToNewSpells(GameplayPlayerIdentity identity)
    {
        if (_spellBonuses.Count == 0) return;
        var stats = identity.stats;
        var spells = stats.GetEquippedSpells();
        if (spells == null) return;
        for (var i = 0; i < spells.Count; i++)
        {
            var spell = spells[i];
            if (!SpellsDone.Add(spell)) continue;
            foreach (var bonus in _spellBonuses)
            {
                if (!PlayerAccess.TryStatType(bonus.Stat, out var type))
                {
                    ModLog.WarnOnce("bonus-stat-" + bonus.Stat, $"MissionBonuses: the game has no stat '{bonus.Stat}'; {bonus.Source} skipped.");
                    continue;
                }

                var before = PlayerAccess.SpellValue(stats, spell, type);
                var added = PlayerAccess.AddSpellBonus(stats, spell, type, bonus);
                ModLog.Info(added == null
                    ? $"MissionBonuses: {spell} has no usable {bonus.Stat}; {bonus.Source} skipped for it."
                    : FormattableString.Invariant($"MissionBonuses: {spell} {bonus.Stat} {before:0.###} -> {PlayerAccess.SpellValue(stats, spell, type):0.###} ({bonus.Source}, +{added.Value:0.###})"));
            }
        }
    }

    /// <summary>Prism: counts the different base elements on the equipped spells and sends the top-up when it grows.</summary>
    private static void UpdatePrism(GameplayPlayerIdentity identity)
    {
        if (_prism == null) return;
        var stats = identity.stats;
        var spells = stats.GetEquippedSpells();
        if (spells == null) return;
        var elements = new List<string>();
        for (var i = 0; i < spells.Count; i++) elements.Add(stats.GetElement(spells[i]).ToString());
        var count = Prism.DistinctBaseElements(elements);
        var add = _prism.Update(count);
        if (!(add > 0f)) return;

        ModLog.StepOnce("bonus-prism", "MissionBonuses: first Prism top-up");
        var bonus = new StatBonus("ElementStrengthMultiplier", add, true, BonusTarget.Character, $"Prism ({count} element(s))");
        if (!TryCharacterModifier(stats, bonus, out var type, out var value)) return;
        PlayerAccess.SendCharacterModifiers(new[] { (type, value) });
        ModLog.Info(FormattableString.Invariant($"MissionBonuses: Prism is now +{_prism.Applied:0}% Elemental Damage ({string.Join(", ", elements)})."));
    }

    /// <summary>Drops the mission state (left the mission, or a new one starts).</summary>
    private static void Forget()
    {
        if (_appliedId != 0) ModLog.Info("MissionBonuses: mission over.");
        _appliedId = 0;
        _readySince = -1f;
        _verifyAt = -1f;
        _spellBonuses = Array.Empty<StatBonus>();
        _prism = null;
        PotionBeltActive = false;
        SpellsDone.Clear();
        VerifyStats.Clear();
    }
}
