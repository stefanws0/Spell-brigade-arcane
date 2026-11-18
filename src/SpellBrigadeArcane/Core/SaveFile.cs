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
}

/// <summary>
/// The mod's own save file (never the game's). Writes go to a temp file first; the previous file becomes the backup.
/// </summary>
public static class SaveFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string BackupPath(string path) => path + ".bak";

    public static string BrokenPath(string path) => path + ".broken";

    public static void Write(string path, SaveData data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
        if (File.Exists(path)) File.Copy(path, BackupPath(path), true);
        File.Move(tmp, path, true);
    }

    /// <summary>
    /// No file: an empty save (first start), no note. Broken or missing file but a good backup: the backup, with a note,
    /// and the broken file is kept as <see cref="BrokenPath"/>. Both broken: an empty save, with a note. Never throws
    /// for a missing or broken file.
    /// </summary>
    public static SaveData Read(string path, out string? note)
    {
        note = null;
        if (!File.Exists(path) && !File.Exists(BackupPath(path))) return new SaveData();
        if (TryParse(path, out var data)) return data!;

        var kept = Path.GetFileName(BrokenPath(path));
        if (File.Exists(path)) File.Copy(path, BrokenPath(path), true);
        if (TryParse(BackupPath(path), out var backup))
        {
            note = $"{Path.GetFileName(path)} is missing or broken (kept as {kept}); loaded the backup.";
            return backup!;
        }

        note = $"{Path.GetFileName(path)} and its backup are broken (kept as {kept}); starting empty.";
        return new SaveData();
    }

    private static bool TryParse(string path, out SaveData? data)
    {
        data = null;
        try
        {
            if (!File.Exists(path)) return false;
            data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            return data != null && data.Paid != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
