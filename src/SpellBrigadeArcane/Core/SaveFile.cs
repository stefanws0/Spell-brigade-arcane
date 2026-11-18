namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// What the mod saves: per enchantment name, the gold paid for each bought level (the level is the list's length).
/// Storing what was paid means refunds stay exact even after prices are retuned.
/// </summary>
public sealed class SaveData
{
    public int Version { get; set; } = 1;

    public Dictionary<string, List<int>> Paid { get; set; } = new();

    /// <summary>
    /// The game's <c>LocalPlayer.lastClearedSaveTimeStamp</c> (ticks) when this was written; null in saves from before
    /// milestone B. It changes when the player resets the game's save.
    /// </summary>
    public long? GameSaveClearedTicks { get; set; }

    /// <summary>
    /// True if this save was written for a game save that has since been reset. Only call it with the stamp of a loaded
    /// game save (the game layer reads it once the Enchantments screen is set up).
    /// </summary>
    public bool IsFromAnotherGameSave(long currentTicks) => GameSaveClearedTicks.HasValue && GameSaveClearedTicks.Value != currentTicks;
}

/// <summary>
/// The mod's own save file (never the game's). Writes go to a temp file first; the previous file becomes the backup.
/// </summary>
public static class SaveFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string BackupPath(string path) => path + ".bak";

    public static string BrokenPath(string path) => path + ".broken";

    /// <summary>
    /// Writes safely: to a flushed temp file first; the current file becomes the backup only if it is a good save (a
    /// broken file never replaces a good backup); then the temp file takes its place. Throws if the file can't be
    /// written or read (e.g. locked), before anything was replaced.
    /// </summary>
    public static void Write(string path, SaveData data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, data, Options);
            stream.Flush(true);
        }

        if (TryParse(path, out _)) File.Copy(path, BackupPath(path), true);
        File.Move(tmp, path, true);
    }

    /// <summary>
    /// No files: an empty save (first start), no note. Main file good: it, no note. Otherwise a note says what happened:
    /// main missing or broken, and the backup good (loaded), broken or missing (starting empty). A broken main file is
    /// kept as <see cref="BrokenPath"/> (only then does the note say so). "Broken" means the content isn't a save (not
    /// JSON, or no "Paid" object). A file that exists but can't be read (locked by another program) throws IOException
    /// or UnauthorizedAccessException: the caller must then leave gold and the save alone.
    /// </summary>
    public static SaveData Read(string path, out string? note)
    {
        note = null;
        var backupPath = BackupPath(path);
        var mainExists = File.Exists(path);
        if (!mainExists && !File.Exists(backupPath)) return new SaveData();
        if (TryParse(path, out var data)) return data!;

        var name = Path.GetFileName(path);
        var kept = Path.GetFileName(BrokenPath(path));
        if (mainExists) File.Copy(path, BrokenPath(path), true);
        var backupExists = File.Exists(backupPath);
        if (TryParse(backupPath, out var backup))
        {
            note = mainExists
                ? $"{name} is broken (kept as {kept}); loaded the backup."
                : $"{name} is missing; loaded the backup.";
            return backup!;
        }

        var backupState = backupExists ? "its backup is broken" : "it has no backup";
        note = mainExists
            ? $"{name} is broken (kept as {kept}) and {backupState}; starting empty."
            : $"{name} is missing and {backupState}; starting empty.";
        return new SaveData();
    }

    private static bool TryParse(string path, out SaveData? data)
    {
        data = null;
        if (!File.Exists(path)) return false;
        var text = File.ReadAllText(path); // I/O errors propagate on purpose: a locked file is not a broken one
        try
        {
            using (var doc = JsonDocument.Parse(text))
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Paid", out var paid) || paid.ValueKind != JsonValueKind.Object)
                    return false;
            }

            data = JsonSerializer.Deserialize<SaveData>(text);
            return data != null && data.Paid != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
