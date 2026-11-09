using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using DiskCleaner.Core.Cleaning;
using static DiskCleaner.Core.Platform.Windows.RestartManagerNative;

namespace DiskCleaner.Core.Platform.Windows;

/// <summary>Uses the Windows Restart Manager to list the processes holding files open.</summary>
[SupportedOSPlatform("windows")]
public sealed class RestartManagerFinder : ILockingProcessFinder
{
    public IReadOnlyList<LockingProcess> FindLockers(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return Array.Empty<LockingProcess>();
        var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
        var rc = RmStartSession(out var session, 0, key);
        if (rc != 0) throw new Win32Exception(rc);
        try
        {
            rc = RmRegisterResources(session, (uint)files.Count, files.ToArray(), 0, null, 0, null);
            if (rc != 0) throw new Win32Exception(rc);

            RM_PROCESS_INFO[]? infos = null;
            uint count = 0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint reasons = 0;
                rc = RmGetList(session, out var needed, ref count, infos, ref reasons);
                if (rc == 0) break;
                if (rc != ERROR_MORE_DATA) throw new Win32Exception(rc);
                count = needed;
                infos = new RM_PROCESS_INFO[needed];
            }
            if (rc != 0 || infos is null || count == 0) return Array.Empty<LockingProcess>();

            var mySession = (uint)Process.GetCurrentProcess().SessionId;
            return infos.Take((int)count).Select(i => ToLocker(i, mySession, files)).ToList();
        }
        finally
        {
            RmEndSession(session);
        }
    }

    private static LockingProcess ToLocker(RM_PROCESS_INFO info, uint mySession, IReadOnlyList<string> files)
    {
        var pid = info.Process.dwProcessId;
        var ft = info.Process.ProcessStartTime;
        long ticks = ((long)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
        DateTime? start = ticks > 0 ? DateTime.FromFileTimeUtc(ticks) : null;
        string name;
        try { using var p = Process.GetProcessById(pid); name = p.ProcessName; }
        catch (ArgumentException) { name = info.strAppName; }
        catch (InvalidOperationException) { name = info.strAppName; }
        var system = info.ApplicationType is RM_APP_TYPE.Service or RM_APP_TYPE.Explorer or RM_APP_TYPE.Critical
                     || info.TSSessionId != mySession;
        var display = string.IsNullOrWhiteSpace(info.strAppName) ? name : info.strAppName;
        return new LockingProcess(pid, name, display, start, system, files);
    }
}
