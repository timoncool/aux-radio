using System.Diagnostics;
using Microsoft.Win32;

namespace GtaRadio;

/// <summary>Ставит в игру SHV + SHVDN + наш мод; всё с бэкапом, без правок RPF и перевода.</summary>
public static class Installer
{
    private static readonly string[] GameExeNames = { "GTA5_Enhanced.exe", "GTA5.exe", "PlayGTAV.exe" };

    public static string? FindGameFromRunning()
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string n = p.ProcessName;
                if (n.Contains("GTA5", StringComparison.OrdinalIgnoreCase) || n.Contains("GTAV", StringComparison.OrdinalIgnoreCase))
                {
                    var file = p.MainModule?.FileName;
                    if (file != null) return Path.GetDirectoryName(file);
                }
            }
            catch { }
        }
        return null;
    }

    public static string? FindGame()
    {
        var running = FindGameFromRunning();
        if (running != null) return running;

        // реестр Rockstar / Steam
        foreach (var key in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                     @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
                     @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV",
                 })
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(key);
                var dir = k?.GetValue("InstallFolder") as string ?? k?.GetValue("InstallLocation") as string;
                if (dir != null && GameExeNames.Any(e => File.Exists(Path.Combine(dir, e)))) return dir;
            }
            catch { }
        }

        // быстрый скан дисков
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady) continue;
                var found = ScanDir(drive.RootDirectory.FullName, 0);
                if (found != null) return found;
            }
            catch { }
        }
        return null;
    }

    private static string? ScanDir(string dir, int depth)
    {
        if (depth > 4) return null;
        try
        {
            foreach (var name in GameExeNames)
                if (File.Exists(Path.Combine(dir, name))) return dir;
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var n = Path.GetFileName(sub).ToLowerInvariant();
                if (n is "windows" or "program files" or "program files (x86)" or "programdata"
                    or "appdata" or "$recycle.bin" or "system volume information") continue;
                var found = ScanDir(sub, depth + 1);
                if (found != null) return found;
            }
        }
        catch { }
        return null;
    }

    public sealed record Result(bool Ok, string Log);

    public static Result Install(string gameDir, string payloadDir)
    {
        var log = new System.Text.StringBuilder();
        try
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backup = Path.Combine(gameDir, "GtaRadio_Backup_" + stamp);
            var map = new (string Src, string Dst)[]
            {
                ("ScriptHookV.dll", "ScriptHookV.dll"),
                ("xinput1_4.dll", "xinput1_4.dll"),
                ("dinput8.dll", "dinput8.dll"),
                ("args.txt", "args.txt"),
                ("ScriptHookVDotNet.asi", "ScriptHookVDotNet.asi"),
                ("ScriptHookVDotNet3.dll", "ScriptHookVDotNet3.dll"),
                ("ScriptHookVDotNet.ini", "ScriptHookVDotNet.ini"),
                ("MinHook.x64.dll", "MinHook.x64.dll"),
                (Path.Combine("mod", "GtaRadioMod.dll"), Path.Combine("scripts", "GtaRadioMod.dll")),
            };

            var skipIfExists = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "xinput1_4.dll", "dinput8.dll", "args.txt"
            };

            foreach (var (src, dst) in map)
            {
                string from = Path.Combine(payloadDir, src);
                if (!File.Exists(from)) { log.AppendLine("нет в payload: " + src); continue; }
                string to = Path.Combine(gameDir, dst);
                if (skipIfExists.Contains(dst) && File.Exists(to))
                {
                    log.AppendLine("уже есть, не трогаем: " + dst);
                    continue;
                }
                if (File.Exists(to))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(backup, dst))!);
                    File.Copy(to, Path.Combine(backup, dst), true);
                    log.AppendLine("бэкап: " + dst);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, true);
                log.AppendLine("установлен: " + dst);
            }

            // агент копируем в папку игры, чтобы всё жило в одном месте
            string self = Process.GetCurrentProcess().MainModule!.FileName!;
            string selfDst = Path.Combine(gameDir, "GtaRadio.exe");
            if (!string.Equals(self, selfDst, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(self, selfDst, true); log.AppendLine("агент: GtaRadio.exe"); } catch { }
            }

            File.WriteAllText(Path.Combine(gameDir, "GtaRadio.installed.txt"),
                $"installed={DateTime.Now:O}\nbackup={backup}\n");
            return new Result(true, log.ToString());
        }
        catch (Exception ex)
        {
            return new Result(false, log + "\nОшибка: " + ex.Message);
        }
    }

    public static void Uninstall(string gameDir)
    {
        try
        {
            foreach (var f in new[]
                     {
                         "ScriptHookVDotNet.asi", "ScriptHookVDotNet3.dll", "ScriptHookVDotNet.ini",
                         "MinHook.x64.dll", "ScriptHookV.dll", Path.Combine("scripts", "GtaRadioMod.dll"),
                         "GtaRadio.installed.txt"
                     })
            {
                try { File.Delete(Path.Combine(gameDir, f)); } catch { }
            }
        }
        catch { }
    }
}
