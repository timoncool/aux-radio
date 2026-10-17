using Windows.Media.Control;

namespace GtaRadio.Agent;

internal sealed record PlayerSession(
    string AppId,
    string Name,
    bool Playing,
    string Artist,
    string Title,
    GlobalSystemMediaTransportControlsSession Session);

/// <summary>Плееры через системные медиа-сессии Windows (SMTC): метаданные и play/pause/next/prev.</summary>
internal static class Smtc
{
    private static GlobalSystemMediaTransportControlsSessionManager? _manager;

    // Известные плееры получают понятное имя и приоритет; остальные SMTC-источники тоже поддерживаются.
    private static readonly (string Token, string Name)[] Known =
    {
        ("spotify", "Spotify"),
        ("yandex", "Яндекс Музыка"),
        ("aimp", "AIMP"),
        ("foobar", "foobar2000"),
        ("vlc", "VLC"),
        ("musicbee", "MusicBee"),
        ("winamp", "Winamp"),
        ("deezer", "Deezer"),
        ("tidal", "TIDAL"),
        ("itunes", "iTunes"),
        ("applemusic", "Apple Music"),
        ("cider", "Cider"),
        ("zunemusic", "Windows Media Player"),
        ("soundcloud", "SoundCloud"),
        ("vk", "VK Музыка"),
    };

    private static readonly string[] Browsers = { "chrome", "msedge", "firefox", "opera", "browser", "brave", "vivaldi" };

    public static bool IsKnownPlayer(string appId) => KnownName(appId) != null;

    private static string? KnownName(string appId)
    {
        string low = appId.ToLowerInvariant();
        foreach (var (token, name) in Known)
            if (low.Contains(token)) return name;
        return null;
    }

    public static string NameFor(string appId)
    {
        var known = KnownName(appId);
        if (known != null) return known;
        string low = appId.ToLowerInvariant();
        foreach (var b in Browsers)
            if (low.Contains(b)) return "Браузер";
        // "AIMP.exe" -> "AIMP", "Pkg_x!App" -> "App", "com.vendor.player" -> "player"
        string s = appId;
        int bang = s.LastIndexOf('!');
        if (bang >= 0) s = s[(bang + 1)..];
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        int dot = s.LastIndexOf('.');
        if (dot >= 0 && dot < s.Length - 1 && !appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[(dot + 1)..];
        return string.IsNullOrWhiteSpace(s) ? appId : s;
    }

    public static async Task<List<PlayerSession>> ListAsync()
    {
        var result = new List<PlayerSession>();
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var s in _manager.GetSessions())
            {
                string appId = s.SourceAppUserModelId ?? "";
                if (appId.Length == 0) continue;
                var name = NameFor(appId);
                bool playing = s.GetPlaybackInfo()?.PlaybackStatus ==
                               GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                string artist = "", title = "";
                try
                {
                    var p = await s.TryGetMediaPropertiesAsync();
                    if (p != null) { artist = p.Artist ?? ""; title = p.Title ?? ""; }
                }
                catch { }
                result.Add(new PlayerSession(appId, name, playing, artist, title, s));
            }
        }
        catch (Exception ex) { Log.W("[ERROR] smtc " + ex.Message); }
        return result;
    }

    /// <summary>Выбор: заданный в настройках, иначе текущий (пока жив), иначе играющий, иначе первый.</summary>
    public static PlayerSession? Choose(List<PlayerSession> list, string? preferred, string? current)
    {
        if (list.Count == 0) return null;
        if (!string.IsNullOrEmpty(preferred))
        {
            var p = list.FirstOrDefault(s => s.AppId.Contains(preferred, StringComparison.OrdinalIgnoreCase) ||
                                             s.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
            if (p != null) return p;
        }
        var cur = current == null ? null : list.FirstOrDefault(s => s.AppId == current);
        if (cur != null && (cur.Playing || !list.Any(s => s.Playing))) return cur;
        // Играющий музыкальный плеер, затем любой играющий источник, затем стоящий на паузе плеер.
        return list.FirstOrDefault(s => s.Playing && IsKnownPlayer(s.AppId))
               ?? list.FirstOrDefault(s => s.Playing)
               ?? list.FirstOrDefault(s => IsKnownPlayer(s.AppId))
               ?? list[0];
    }
}
