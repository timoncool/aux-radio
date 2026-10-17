using System.Text.Json;
using NAudio.CoreAudioApi;

namespace GtaRadio.Agent;

/// <summary>
/// Только восстановление громкости сессий, которую мог изменить старый клиент 1.x.
/// Новый агент громкость плееров не трогает вообще.
/// </summary>
internal static class SessionVolumes
{
    private sealed class Saved
    {
        public string Sid { get; set; } = "";
        public string Iid { get; set; } = "";
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

    /// <summary>Возвращает громкость, которую менял старый клиент, и очищает журнал.</summary>
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
