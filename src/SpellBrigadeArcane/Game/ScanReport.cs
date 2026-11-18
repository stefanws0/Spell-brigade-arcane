namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

/// <summary>Writes BepInEx/SpellBrigadeArcane-scan.txt, started fresh each session, one section per scan.</summary>
internal static class ScanReport
{
    private static bool _started;

    public static string FilePath => Path.Combine(Paths.BepInExRootPath, "SpellBrigadeArcane-scan.txt");

    public static void Section(string title, IEnumerable<string> lines)
    {
        try
        {
            if (!_started)
            {
                File.WriteAllText(FilePath, $"SpellBrigadeArcane {Plugin.Version} scan, {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
                _started = true;
            }

            var sb = new StringBuilder();
            sb.AppendLine().AppendLine($"== {title} ({DateTime.Now:HH:mm:ss}) ==");
            foreach (var line in lines) sb.AppendLine(line);
            File.AppendAllText(FilePath, sb.ToString());
            ModLog.Info($"Scan: wrote '{title}' to {Path.GetFileName(FilePath)}");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Scan: could not write {FilePath}: {ex.Message}");
        }
    }
}
