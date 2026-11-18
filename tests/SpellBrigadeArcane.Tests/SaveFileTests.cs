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
        Assert.Equal("save.json is broken (kept as save.json.broken); loaded the backup.", note);
        Assert.True(File.Exists(SaveFile.BrokenPath(SavePath)));
    }

    [Fact]
    public void Read_MissingMain_LoadsTheBackup_WithoutClaimingABrokenCopy()
    {
        SaveFile.Write(SavePath, With("Reach", 5));
        SaveFile.Write(SavePath, With("Reach", 5, 6));
        File.Delete(SavePath);
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Equal(new[] { 5 }, data.Paid["Reach"]);
        Assert.Equal("save.json is missing; loaded the backup.", note);
        Assert.DoesNotContain("kept as", note);
        Assert.False(File.Exists(SaveFile.BrokenPath(SavePath)));
    }

    [Fact]
    public void Read_MissingMain_BrokenBackup_StartsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SaveFile.BackupPath(SavePath), "y");
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Empty(data.Paid);
        Assert.Equal("save.json is missing and its backup is broken; starting empty.", note);
        Assert.False(File.Exists(SaveFile.BrokenPath(SavePath)));
    }

    [Fact]
    public void Read_BrokenMain_WithoutBackup_StartsEmpty_AndKeepsTheBrokenCopy()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, "x");
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Empty(data.Paid);
        Assert.Equal("save.json is broken (kept as save.json.broken) and it has no backup; starting empty.", note);
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
        Assert.Equal("save.json is broken (kept as save.json.broken) and its backup is broken; starting empty.", note);
    }

    [Fact]
    public void Read_EmptyObject_CountsAsBroken()
    {
        SaveFile.Write(SavePath, With("Volley", 7));
        SaveFile.Write(SavePath, With("Volley", 7, 8));
        File.WriteAllText(SavePath, "{}");
        var data = SaveFile.Read(SavePath, out var note);
        Assert.Equal(new[] { 7 }, data.Paid["Volley"]);
        Assert.Contains("backup", note);
    }

    [Fact]
    public void Write_NeverReplacesAGoodBackupWithABrokenFile()
    {
        SaveFile.Write(SavePath, With("Volley", 1));
        SaveFile.Write(SavePath, With("Volley", 1, 2));
        File.WriteAllText(SavePath, "x");
        SaveFile.Write(SavePath, With("Volley", 1, 2, 3));
        var backup = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SaveFile.BackupPath(SavePath)))!;
        Assert.Equal(new[] { 1 }, backup.Paid["Volley"]);
    }

    [Fact]
    public void Read_LockedFile_Throws_AndKeepsNothingAsBroken()
    {
        SaveFile.Write(SavePath, With("Reach", 5));
        using (new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => SaveFile.Read(SavePath, out _));
        }

        Assert.False(File.Exists(SaveFile.BrokenPath(SavePath)));
    }

    [Fact]
    public void GameSaveStamp_RoundTrips()
    {
        var data = With("Volley", 100);
        data.GameSaveClearedTicks = 638_670_000_000_000_000L;
        SaveFile.Write(SavePath, data);
        Assert.Equal(638_670_000_000_000_000L, SaveFile.Read(SavePath, out _).GameSaveClearedTicks);
    }

    [Fact]
    public void GameSaveStamp_IsNullInOldSaves()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, "{\"Version\":1,\"Paid\":{\"Volley\":[100]}}");
        Assert.Null(SaveFile.Read(SavePath, out _).GameSaveClearedTicks);
    }

    [Theory]
    [InlineData(null, 5L, false)]
    [InlineData(5L, 5L, false)]
    [InlineData(5L, 6L, true)]
    public void IsFromAnotherGameSave_OnlyWhenTheStampDiffers(long? stored, long current, bool expected) =>
        Assert.Equal(expected, new SaveData { GameSaveClearedTicks = stored }.IsFromAnotherGameSave(current));
}
