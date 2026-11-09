namespace DiskCleaner.Core.Cleaning;

/// <param name="Name">Process name (e.g. "chrome").</param>
/// <param name="DisplayName">Friendly name shown to the user (e.g. "Google Chrome").</param>
/// <param name="StartTimeUtc">Used to make sure the PID still belongs to the same process when closing.</param>
/// <param name="IsSystem">Services, critical processes, Explorer, other sessions: never closed.</param>
/// <param name="Paths">Files this process was found holding.</param>
public sealed record LockingProcess(int Id, string Name, string DisplayName, DateTime? StartTimeUtc, bool IsSystem, IReadOnlyList<string> Paths);

public interface ILockingProcessFinder
{
    /// <summary>Processes holding any of the given files open.</summary>
    IReadOnlyList<LockingProcess> FindLockers(IReadOnlyList<string> files);
}

public interface IProcessCloser
{
    /// <summary>Asks each process to close normally, then force-closes those still running after <paramref name="grace"/>.
    /// Returns the processes that could not be closed.</summary>
    IReadOnlyList<LockingProcess> CloseAll(IReadOnlyList<LockingProcess> processes, TimeSpan grace);
}

public static class ProtectedProcesses
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "explorer", "dwm", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "RuntimeBroker",
        "SearchHost", "SearchIndexer", "StartMenuExperienceHost", "ShellExperienceHost", "TextInputHost",
        "MsMpEng", "NisSrv", "SecurityHealthService", "spoolsv", "audiodg", "conhost", "dllhost", "WmiPrvSE",
    };

    public static bool IsProtectedName(string processName)
    {
        var n = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;
        return Names.Contains(n);
    }
}
