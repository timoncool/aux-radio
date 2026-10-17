using System.Diagnostics;

namespace GtaRadio;

public static class GameWatcher
{
    private static readonly string[] Markers = { "GTA5", "GTAV", "Grand Theft Auto" };

    public static Process? Find(out string? path)
    {
        path = null;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                if (!Markers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase))) continue;
                if (p.MainWindowHandle == IntPtr.Zero && p.ProcessName.Contains("Play", StringComparison.OrdinalIgnoreCase)) continue;
                path = p.MainModule?.FileName;
                return p;
            }
            catch { }
        }
        return null;
    }

    public static bool IsRunning() => Find(out _) != null;
}
