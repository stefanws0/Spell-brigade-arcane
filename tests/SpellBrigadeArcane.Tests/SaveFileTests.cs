namespace SpellBrigadeArcane.Tests;

using System.Text.Json;
using SpellBrigadeArcane.Core;
using Xunit;

public sealed class SaveFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arcane-tests-" + Guid.NewGuid().ToString("N"));

    private string SavePath => Path.Combine(_dir, "sub", "save.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static SaveData With(string name, params int[] paid) => new() { Paid = { [name] = paid.ToList() } };

    [Fact]
    public void Read_WithoutAFile_IsEmptyAndQuiet()
    {
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Empty(data.Paid);
        Assert.Null(note);
    }

    [Fact]
    public void Write_ThenRead_RoundTrips_AndCreatesTheFolder()
    {
        SaveFile.Write(SavePath, With("Volley", 100, 150));
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Equal(new[] { 100, 150 }, data.Paid["Volley"]);
        Assert.Null(note);
    }

    [Fact]
    public void Write_KeepsThePreviousFileAsBackup()
    {
        SaveFile.Write(SavePath, With("Volley", 1));
        SaveFile.Write(SavePath, With("Volley", 1, 2));
        var backup = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SaveFile.BackupPath(SavePath)))!;
        Assert.Equal(new[] { 1 }, backup.Paid["Volley"]);
    }

    [Fact]
    public void Read_BrokenFile_LoadsTheBackup_AndKeepsTheBrokenCopy()
    {
        SaveFile.Write(SavePath, With("Reach", 5));
        SaveFile.Write(SavePath, With("Reach", 5, 6));
        File.WriteAllText(SavePath, "{ not json");
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Equal(new[] { 5 }, data.Paid["Reach"]);
        Assert.Contains("backup", note);
        Assert.True(File.Exists(SaveFile.BrokenPath(SavePath)));
    }

    [Fact]
    public void Read_BothBroken_StartsEmptyWithANote()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, "x");
        File.WriteAllText(SaveFile.BackupPath(SavePath), "y");
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Empty(data.Paid);
        Assert.Contains("broken", note);
    }
}
