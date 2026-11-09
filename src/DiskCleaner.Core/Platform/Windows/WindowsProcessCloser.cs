using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using DiskCleaner.Core.Cleaning;

namespace DiskCleaner.Core.Platform.Windows;

/// <summary>Closes processes: first asks politely (close main window, like clicking X), then kills those still running.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProcessCloser : IProcessCloser
{
    public IReadOnlyList<LockingProcess> CloseAll(IReadOnlyList<LockingProcess> processes, TimeSpan grace)
    {
        var failed = new List<LockingProcess>();
        var running = new List<(LockingProcess Info, Process Proc)>();
        foreach (var info in processes)
        {
            var proc = Open(info);
            if (proc is null) continue; // already gone (or the PID now belongs to another process)
            running.Add((info, proc));
            try { proc.CloseMainWindow(); } catch (InvalidOperationException) { }
        }

        var deadline = DateTime.UtcNow + grace;
        while (DateTime.UtcNow < deadline && running.Any(r => !HasExited(r.Proc)))
            Thread.Sleep(200);

        foreach (var (info, proc) in running)
        {
            using (proc)
            {
                if (HasExited(proc)) continue;
                try
                {
                    proc.Kill();
                    if (!proc.WaitForExit(3000)) failed.Add(info);
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    if (!HasExited(proc)) failed.Add(info);
                }
            }
        }
        return failed;
    }

    private static Process? Open(LockingProcess info)
    {
        try
        {
            var p = Process.GetProcessById(info.Id);
            if (info.StartTimeUtc is { } expected && Math.Abs((p.StartTime.ToUniversalTime() - expected).TotalSeconds) > 2)
            {
                p.Dispose();
                return null;
            }
            return p;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return false; }
    }
}
