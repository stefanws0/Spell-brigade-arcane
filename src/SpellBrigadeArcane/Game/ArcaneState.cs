namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using SpellBrigadeArcane.Core;

/// <summary>
/// The sixth tier's progress for the current game save, kept in the mod's own file
/// (BepInEx/config/SpellBrigadeArcane/save.json). Load rules from the spec: a reset of the game's save clears it; levels
/// above the max level are refunded (trimmed save written first, so they can't be refunded twice); a save that can't
/// be read now (e.g. locked) keeps the shop closed until it can.
/// </summary>
internal static class ArcaneState
{
    public static readonly IReadOnlyList<EnchantmentDef> Defs = Catalog.Build();

    private static long _gameStamp;

    /// <summary>Null until loaded, and while the save can't be read.</summary>
    public static Progress? Current { get; private set; }

    public static string SavePath => Path.Combine(Paths.ConfigPath, "SpellBrigadeArcane", "save.json");

    public static EnchantmentDef Def(ArcaneId id) => Catalog.Get(Defs, id);

    public static bool IsUnlocked(int masteries) =>
        Progress.IsUnlocked(masteries, Progress.UnlockMasteries, ModConfig.UnlockRow.Value);

    /// <summary>Loads once (until <see cref="Discard"/>). False if the save or the game isn't readable right now.</summary>
    public static bool EnsureLoaded()
    {
        if (Current != null) return true;
        ModLog.StepOnce("arcane-load", "ArcaneState: first load of " + SavePath);
        SaveData data;
        try
        {
            data = SaveFile.Read(SavePath, out var note);
            if (note != null) ModLog.Warn("ArcaneState: " + note);
            _gameStamp = GameUpgrades.SaveClearedTicks;
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("arcane-load-failed", $"ArcaneState: can't load the sixth tier yet ({ex.GetType().Name}: {ex.Message}); retrying.");
            return false;
        }

        if (data.IsFromAnotherGameSave(_gameStamp))
        {
            ModLog.Warn("ArcaneState: the game's save was reset since the sixth tier was saved; its levels are cleared (no gold back: the reset took the gold too).");
            data = new SaveData();
        }

        var progress = Progress.FromSave(Defs, data, out var excess, out var notes);
        foreach (var n in notes) ModLog.Warn("ArcaneState: " + n);
        Current = progress;
        if (!Save())
        {
            Current = null;
            return false;
        }

        if (excess > 0)
        {
            GameUpgrades.Give(excess);
            GameUpgrades.SaveGame();
            ModLog.Info($"ArcaneState: refunded {excess} gold for levels above the max level.");
        }

        ModLog.Info($"ArcaneState: loaded {progress.TotalLevels} sixth-tier level(s), {progress.TotalPaid} gold paid in total.");
        return true;
    }

    /// <summary>Writes the save; false (and logged) if it couldn't.</summary>
    public static bool Save()
    {
        if (Current == null) return false;
        try
        {
            var data = Current.ToSave();
            data.GameSaveClearedTicks = _gameStamp;
            SaveFile.Write(SavePath, data);
            return true;
        }
        catch (Exception ex)
        {
            ModLog.Error($"ArcaneState: could not write {SavePath}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Forgets the in-memory books, so the next <see cref="EnsureLoaded"/> reads the file again.</summary>
    public static void Discard() => Current = null;
}
