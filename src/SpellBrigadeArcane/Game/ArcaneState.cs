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

    private const float WaitSeconds = 1f;  // while the game's save isn't loaded yet
    private const float ErrorSeconds = 5f; // after a file error

    private static long? _gameStamp;
    private static float _nextTry;
    private static bool _firstLoadLogged;

    /// <summary>Null until loaded, and while the save can't be read.</summary>
    public static Progress? Current { get; private set; }

    public static string SavePath => Path.Combine(Paths.ConfigPath, "SpellBrigadeArcane", "save.json");

    public static EnchantmentDef Def(ArcaneId id) => Catalog.Get(Defs, id);

    public static bool IsUnlocked(int masteries) =>
        Progress.IsUnlocked(masteries, Progress.UnlockMasteries, ModConfig.UnlockRow.Value);

    private static string AsDate(long? ticks)
    {
        if (ticks == null) return "none";
        try
        {
            return new DateTime(ticks.Value).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + $" ({ticks.Value})";
        }
        catch (Exception)
        {
            return ticks.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Loads once (until <see cref="Discard"/>), trying again every 1 s while the game loads and every 5 s after a file error. False if the game's save isn't loaded
    /// yet or ours isn't readable right now.
    /// </summary>
    public static bool EnsureLoaded()
    {
        if (Current != null) return true;
        var now = UnityEngine.Time.unscaledTime;
        if (now < _nextTry) return false;
        _nextTry = now + ErrorSeconds;

        if (!GameUpgrades.IsReady)
        {
            _nextTry = now + WaitSeconds;
            ModLog.InfoOnce("arcane-wait", "ArcaneState: waiting for the game's save to load");
            return false;
        }

        ModLog.StepOnce("arcane-load", "ArcaneState: first load of " + SavePath);
        SaveData data;
        try
        {
            data = SaveFile.Read(SavePath, out var note);
            if (note != null) ModLog.WarnOnce("arcane-load-note-" + note, "ArcaneState: " + note);
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("arcane-load-failed", $"ArcaneState: can't load the sixth tier yet ({ex.GetType().Name}: {ex.Message}); retrying.");
            return false;
        }

        long stamp;
        try
        {
            stamp = GameUpgrades.SaveClearedTicks;
            if (!_firstLoadLogged)
            {
                _firstLoadLogged = true;
                ModLog.Info($"ArcaneState: game is initialised; save stamp {AsDate(stamp)}, gold {GameUpgrades.Gold}, {GameUpgrades.TotalBought} level(s) bought.");
            }
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("arcane-stamp-failed", $"ArcaneState: can't read the game's save stamp yet ({ex.GetType().Name}: {ex.Message}); retrying.");
            return false;
        }

        var r = LoadRules.Decide(Defs, data, stamp);
        foreach (var n in r.Notes) ModLog.WarnOnce("arcane-note-" + n, "ArcaneState: " + n);
        if (r.WasReset)
        {
            var old = data.GameSaveClearedTicks;
            var oldKey = old?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";
            var copy = SavePath + ".reset-" + oldKey;
            try
            {
                // The dropped data itself (it may have come from the backup), not whatever file sits at SavePath.
                SaveFile.Write(copy, data);
            }
            catch (Exception ex)
            {
                ModLog.WarnOnce("arcane-reset-copy-" + oldKey, $"ArcaneState: could not keep a copy of the old save at {copy} ({ex.GetType().Name}: {ex.Message}); retrying, the reset can't be undone without it.");
                return false;
            }

            var dropped = Progress.FromSave(Defs, data, out _, out _);
            ModLog.WarnOnce("arcane-reset-" + oldKey, $"ArcaneState: the game's save was reset (our save was for {AsDate(old)}, the game's is now {AsDate(stamp)}); dropping {dropped.TotalLevels} sixth-tier level(s) / {dropped.TotalPaid} gold (no gold back: the reset took the gold too). Old file kept at {copy}.");
        }

        // Retry later rather than give gold the game can't save.
        if (r.ExcessGold > 0 && !GameUpgrades.CanSaveGame)
        {
            ModLog.InfoOnce("arcane-excess-wait", "ArcaneState: waiting for the game to be able to save before refunding excess gold.");
            return false;
        }

        _gameStamp = r.StampToWrite;
        Current = r.Progress;
        if (!Save())
        {
            Current = null;
            return false;
        }

        if (r.ExcessGold > 0)
        {
            try
            {
                ModLog.StepOnce("arcane-excess", "ArcaneState: refunding excess gold");
                GameUpgrades.Give(r.ExcessGold);
                if (GameUpgrades.SaveGame()) MarkDirty(); // the trimmed save is already written; this just re-writes it after the game's save
                ModLog.Info($"ArcaneState: refunded {r.ExcessGold} gold for levels above the max level.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"ArcaneState: refunding {r.ExcessGold} gold for levels above the max level failed ({ex.GetType().Name}: {ex.Message}); add that gold back by hand.");
            }
        }

        ModLog.Info($"ArcaneState: loaded {r.Progress.TotalLevels} sixth-tier level(s), {r.Progress.TotalPaid} gold paid in total.");
        return true;
    }

    /// <summary>
    /// Call regularly: if the game's save was swapped or reset mid-session, forgets the books so the next
    /// <see cref="EnsureLoaded"/> applies the reset rules; adopts the stamp if we had none.
    /// </summary>
    public static void CheckGameSave()
    {
        if (Current == null || !GameUpgrades.IsReady) return;
        long s;
        try
        {
            s = GameUpgrades.SaveClearedTicks;
        }
        catch (Exception)
        {
            return;
        }

        if (s <= 0) return;
        if (_gameStamp == null)
        {
            _gameStamp = s;
        }
        else if (s != _gameStamp)
        {
            ModLog.Warn("ArcaneState: the game's save changed during the session; reloading the sixth tier.");
            Discard();
        }
    }

    private static bool _dirty;
    private static bool _flushFailed;
    private static bool _saveFailing;
    private static int _dirtyFrame;
    private static float _dirtySince;
    private static float _nextFlush;

    /// <summary>Our file is (re)written at least 2 frames and 0.5 s from now, after the game's own save (it saves in the frame after the trigger).</summary>
    public static void MarkDirty()
    {
        _dirty = true;
        _dirtyFrame = UnityEngine.Time.frameCount;
        _dirtySince = UnityEngine.Time.unscaledTime;
    }

    /// <summary>Call every frame: writes our save if <see cref="MarkDirty"/> asked for it; retries on failure.</summary>
    public static void FlushIfDirty()
    {
        if (!_dirty) return;
        // The game's save coroutine writes in the frame after the trigger, after all Updates: wait at least 2 frames and 0.5 s.
        if (UnityEngine.Time.frameCount < _dirtyFrame + 2 || UnityEngine.Time.unscaledTime < _dirtySince + 0.5f) return;
        if (_flushFailed && UnityEngine.Time.unscaledTime < _nextFlush) return;
        if (Current == null)
        {
            _dirty = false; // the books were discarded and will be reloaded from the file
            return;
        }

        if (Save())
        {
            _dirty = false;
            _flushFailed = false;
        }
        else if (!_flushFailed)
        {
            _flushFailed = true;
            ModLog.Warn("ArcaneState: the delayed save failed; retrying every second until it works.");
        }

        if (_dirty) _nextFlush = UnityEngine.Time.unscaledTime + 1f;
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
            _dirty = false;
            _saveFailing = false;
            return true;
        }
        catch (Exception ex)
        {
            if (!_saveFailing)
            {
                _saveFailing = true; // logged once per failure streak; FlushIfDirty retries every second
                ModLog.Error($"ArcaneState: could not write {SavePath}: {ex.GetType().Name}: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>Forgets the in-memory books, so the next <see cref="EnsureLoaded"/> reads the file again.</summary>
    public static void Discard()
    {
        Current = null;
        _nextTry = 0f;
    }
}
