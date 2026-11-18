namespace SpellBrigadeArcane.Core;

using System.Collections.Generic;

/// <summary>What loading the mod's save decided.</summary>
public sealed class LoadResult
{
    public LoadResult(Progress progress, int excessGold, bool wasReset, long? stampToWrite, List<string> notes)
    {
        Progress = progress;
        ExcessGold = excessGold;
        WasReset = wasReset;
        StampToWrite = stampToWrite;
        Notes = notes;
    }

    public Progress Progress { get; }

    /// <summary>Gold to give back for levels above the max level (0 after a reset).</summary>
    public int ExcessGold { get; }

    /// <summary>The game's save was reset since ours was written; the levels were dropped.</summary>
    public bool WasReset { get; }

    /// <summary>The game-save stamp our file should carry; null if neither side knows one.</summary>
    public long? StampToWrite { get; }

    public List<string> Notes { get; }
}

public static class LoadRules
{
    /// <param name="currentStamp">The game's lastClearedSaveTimeStamp ticks; 0 or less means unknown (never a reset).</param>
    public static LoadResult Decide(IReadOnlyList<EnchantmentDef> defs, SaveData data, long currentStamp)
    {
        var known = currentStamp > 0;
        var reset = known && data.IsFromAnotherGameSave(currentStamp);
        var progress = Progress.FromSave(defs, reset ? new SaveData() : data, out var excess, out var notes);
        return new LoadResult(progress, reset ? 0 : excess, reset, known ? currentStamp : data.GameSaveClearedTicks, notes);
    }
}
