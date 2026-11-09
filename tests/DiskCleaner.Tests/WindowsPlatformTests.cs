using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.IO;
using DiskCleaner.Core.Platform.Windows;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

/// <summary>Run only on Windows (CI); return early elsewhere.</summary>
public class WindowsPlatformTests
{
    [Fact]
    public void WindowsFileSystem_AllocatedSize_EqualsLengthForNormalFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var path = d.File("a.bin", 12345);
        var fs = new WindowsFileSystem();
        var entry = fs.GetEntry(path)!;
        Assert.Equal(12345, fs.GetAllocatedSize(entry));
    }

    [Fact]
    public void WindowsFileSystem_EnumeratesLongPaths_WithoutPrefixInResults()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var deep = string.Join('\\', Enumerable.Repeat(new string('x', 50), 6));
        var file = d.File(Path.Combine(deep, "f.bin"), 3);
        var fs = new WindowsFileSystem();
        var entry = Assert.Single(fs.Enumerate(Path.GetDirectoryName(file)!));
        Assert.Equal(file, entry.FullPath);
    }

    [Fact]
    public void ShellRecycleBin_Recycle_TempFile_Recycled_AndFileGone()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var path = d.File("recycle-me.txt", 10);
        var bin = new ShellRecycleBin();
        Assert.True(bin.CanRecycle(path));
        Assert.Equal(RecycleOutcome.Recycled, bin.Recycle(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ShellRecycleBin_Recycle_LockedFile_NotRecycled()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var path = d.File("locked.txt", 10);
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var outcome = new ShellRecycleBin().Recycle(path);
            Assert.NotEqual(RecycleOutcome.Recycled, outcome);
            Assert.True(File.Exists(path));
        }
    }

    [Fact]
    public void ShellRecycleBin_CanRecycle_FalseForVeryLongPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.False(new ShellRecycleBin().CanRecycle(@"C:\" + new string('a', 300)));
    }
}

public class WindowsProcessTests
{
    [Fact]
    public void RestartManager_FindsThisProcess_HoldingAFile()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        var path = d.File("held.txt", 10);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var lockers = new RestartManagerFinder().FindLockers(new[] { path });
        Assert.Contains(lockers, l => l.Id == Environment.ProcessId);
    }

    [Fact]
    public void RestartManager_UnlockedFile_NoLockers()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var d = new TempDir();
        Assert.Empty(new RestartManagerFinder().FindLockers(new[] { d.File("free.txt", 10) }));
    }

    [Fact]
    public void Closer_ForceClosesProcessWithoutWindow()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ping", "-n 120 127.0.0.1")
            { CreateNoWindow = true, UseShellExecute = false })!;
        var info = new DiskCleaner.Core.Cleaning.LockingProcess(p.Id, "PING", "ping", p.StartTime.ToUniversalTime(), false, Array.Empty<string>());
        var failed = new WindowsProcessCloser().CloseAll(new[] { info }, TimeSpan.FromSeconds(1));
        Assert.Empty(failed);
        Assert.True(p.WaitForExit(5000));
    }
}
