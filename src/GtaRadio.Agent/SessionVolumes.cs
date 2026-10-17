using System.Text.Json;
using NAudio.CoreAudioApi;

namespace GtaRadio.Agent;

/// <summary>
/// Громкость сессий плеера в микшере Windows. Меняется только громкость приложения
/// (как ползунок в «Микшере громкости»), не устройства. Исходные значения пишутся в журнал
/// %LOCALAPPDATA%\GtaRadio\sessions.json до изменения; RestoreAll() и старт клиента
/// возвращают их, даже если прошлый запуск был убит.
/// </summary>
internal static class SessionVolumes
{
    private sealed class Saved
    {
        public string Sid { get; set; } = "";   // GetSessionIdentifier: стабилен между запусками приложения
        public string Iid { get; set; } = "";   // GetSessionInstanceIdentifier: конкретный экземпляр
        public float Volume { get; set; }
        public bool Mute { get; set; }
        public float Applied { get; set; }
    }

    private static readonly object Lock = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio", "sessions.json");
    private static List<Saved>? _saved;

    private static List<Saved> Journal()
    {
        if (_saved != null) return _saved;
        try
        {
            if (File.Exists(FilePath))
                _saved = JsonSerializer.Deserialize<List<Saved>>(File.ReadAllText(FilePath));
        }
        catch { }
        return _saved ??= new List<Saved>();
    }

    private static void Flush()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (_saved == null || _saved.Count == 0) { if (File.Exists(FilePath)) File.Delete(FilePath); return; }
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_saved));
        }
        catch { }
    }

    private static IEnumerable<AudioSessionControl> Sessions(MMDeviceEnumerator en)
    {
        foreach (var dev in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            SessionCollection? ss = null;
            try { dev.AudioSessionManager.RefreshSessions(); ss = dev.AudioSessionManager.Sessions; } catch { }
            if (ss == null) continue;
            for (int i = 0; i < ss.Count; i++) yield return ss[i];
        }
    }

    /// <summary>
    /// Ставит сессиям процессов pids громкость level (0..1) относительно исходной.
    /// Возвращает исходную громкость первой найденной сессии (для компенсации усиления).
    /// </summary>
    public static float Set(HashSet<uint> pids, float level)
    {
        lock (Lock)
        {
            var journal = Journal();
            float original = -1;
            bool changed = false;
            try
            {
                using var en = new MMDeviceEnumerator();
                foreach (var s in Sessions(en))
                {
                    try
                    {
                        if (!pids.Contains(s.GetProcessID)) continue;
                        string iid = s.GetSessionInstanceIdentifier;
                        var saved = journal.FirstOrDefault(x => x.Iid == iid);
                        var vol = s.SimpleAudioVolume;
                        if (saved == null)
                        {
                            saved = new Saved { Sid = s.GetSessionIdentifier, Iid = iid, Volume = vol.Volume, Mute = vol.Mute };
                            journal.Add(saved);
                            changed = true;
                        }
                        float target = Math.Clamp(saved.Volume * level, 0f, 1f);
                        if (Math.Abs(vol.Volume - target) > 0.0001f) vol.Volume = target;
                        if (saved.Applied != target) { saved.Applied = target; changed = true; }
                        if (original < 0) original = saved.Volume;
                    }
                    catch { }
                }
            }
            catch { }
            if (changed) Flush();
            return original < 0 ? 1f : original;
        }
    }

    /// <summary>Процессы, у которых сессия сейчас реально звучит (пиковый уровень выше порога).</summary>
    public static HashSet<uint> LoudPids(float threshold = 0.001f)
    {
        var result = new HashSet<uint>();
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var s in Sessions(en))
            {
                try
                {
                    if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive &&
                        s.AudioMeterInformation.MasterPeakValue > threshold)
                        result.Add(s.GetProcessID);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    /// <summary>Возвращает всё, что меняли (в том числе после падения прошлого запуска).</summary>
    public static void RestoreAll()
    {
        lock (Lock)
        {
            var journal = Journal();
            if (journal.Count == 0) return;
            try
            {
                using var en = new MMDeviceEnumerator();
                foreach (var s in Sessions(en))
                {
                    try
                    {
                        string iid = s.GetSessionInstanceIdentifier;
                        var saved = journal.FirstOrDefault(x => x.Iid == iid);
                        // Плеер перезапущен после падения: Windows помнит громкость приложения по Sid,
                        // поэтому возвращаем по Sid, если громкость всё ещё та, что ставили мы.
                        saved ??= journal.FirstOrDefault(x => x.Sid == s.GetSessionIdentifier &&
                                                              Math.Abs(s.SimpleAudioVolume.Volume - x.Applied) < 0.002f);
                        if (saved == null) continue;
                        s.SimpleAudioVolume.Volume = saved.Volume;
                        s.SimpleAudioVolume.Mute = saved.Mute;
                    }
                    catch { }
                }
            }
            catch { }
            journal.Clear();
            Flush();
        }
    }
}
