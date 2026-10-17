using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GtaRadio.Agent;

/// <summary>Дерево процессов через Toolhelp32: корень плеера и его потомки.</summary>
internal static class ProcessTree
{
    private const uint TH32CS_SNAPPROCESS = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);

    public readonly record struct Entry(uint Pid, uint Parent, string Exe);

    public static List<Entry> Snapshot()
    {
        var list = new List<Entry>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return list;
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snap, ref e)) return list;
            do list.Add(new Entry(e.th32ProcessID, e.th32ParentProcessID, Path.GetFileNameWithoutExtension(e.szExeFile)));
            while (Process32NextW(snap, ref e));
        }
        finally { CloseHandle(snap); }
        return list;
    }

    /// <summary>pid и все его потомки.</summary>
    public static HashSet<uint> Descendants(uint root)
    {
        var all = Snapshot();
        var result = new HashSet<uint> { root };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var e in all)
                if (!result.Contains(e.Pid) && result.Contains(e.Parent) && e.Pid != 0) { result.Add(e.Pid); grew = true; }
        }
        return result;
    }

    // Имена exe известных плееров по SMTC AppId (у Яндекса AppId — AUMID, а не имя exe).
    private static readonly (string Token, string[] Exe)[] KnownExe =
    {
        ("spotify", new[] { "Spotify" }),
        ("yandex", new[] { "Яндекс Музыка", "YandexMusic", "Yandex Music" }),
        ("aimp", new[] { "AIMP" }),
        ("foobar", new[] { "foobar2000" }),
        ("vlc", new[] { "vlc" }),
        ("musicbee", new[] { "MusicBee" }),
        ("winamp", new[] { "winamp" }),
        ("deezer", new[] { "Deezer" }),
        ("tidal", new[] { "TIDAL" }),
        ("itunes", new[] { "iTunes" }),
        ("cider", new[] { "Cider" }),
        ("zunemusic", new[] { "Microsoft.Media.Player", "Music.UI" }),
    };

    private static readonly HashSet<string> SystemExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "audiodg", "svchost", "explorer", "dwm", "csrss", "ShellExperienceHost", "SystemSettings"
    };

    /// <summary>
    /// Корневой процесс плеера для SMTC-сессии. Захват с INCLUDE_TARGET_PROCESS_TREE от корня
    /// берёт звук всех дочерних процессов (Electron/CEF/браузеры играют звук из дочернего).
    /// Порядок: имя exe из AppId -> известные плееры -> совпадение частей AppId с exe ->
    /// процесс, у которого сейчас звучит аудиосессия (loudPids).
    /// </summary>
    public static uint FindPlayerRoot(string appId, uint gamePid, HashSet<uint> loudPids)
    {
        uint self = (uint)Environment.ProcessId;
        var all = Snapshot();
        var byPid = new Dictionary<uint, Entry>();
        foreach (var e in all) byPid[e.Pid] = e;
        bool Usable(Entry e) => e.Pid != self && e.Pid != gamePid && e.Pid > 4 && !SystemExe.Contains(e.Exe);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            names.Add(Path.GetFileNameWithoutExtension(appId.Replace('/', '\\').Split('\\').Last()));
        string low = appId.ToLowerInvariant();
        foreach (var (token, exe) in KnownExe)
            if (low.Contains(token)) foreach (var n in exe) names.Add(n);

        var matched = all.Where(e => Usable(e) && names.Contains(e.Exe)).ToList();
        if (matched.Count == 0)
        {
            var parts = appId.Split(new[] { '\\', '/', '!', '.', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 4 && !Generic.Contains(t)).ToList();
            matched = all.Where(e => Usable(e) && parts.Any(t => e.Exe.Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        if (matched.Count == 0)
            matched = all.Where(e => Usable(e) && loudPids.Contains(e.Pid)).ToList();
        if (matched.Count == 0) return 0;

        // Поднимаемся к корню: пока родитель — тот же exe.
        var roots = new List<uint>();
        foreach (var m in matched)
        {
            var cur = m;
            while (byPid.TryGetValue(cur.Parent, out var parent) && Usable(parent) &&
                   string.Equals(parent.Exe, cur.Exe, StringComparison.OrdinalIgnoreCase))
                cur = parent;
            if (!roots.Contains(cur.Pid)) roots.Add(cur.Pid);
        }
        if (roots.Count == 1) return roots[0];

        // Несколько корней: тот, в чьём дереве сейчас звучит сессия, иначе тот, у кого есть окно.
        foreach (var r in roots)
            if (Descendants(r).Overlaps(loudPids)) return r;
        foreach (var r in roots)
        {
            try { if (Process.GetProcessById((int)r).MainWindowHandle != IntPtr.Zero) return r; }
            catch { }
        }
        return roots[0];
    }

    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop", "music", "player", "app", "exe", "com", "microsoft", "windows", "media"
    };
}
