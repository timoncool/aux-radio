using Windows.Media.Control;

namespace GtaRadio;

public sealed record SmtcSession(
    string AppId,
    string Name,
    bool Playing,
    string Title,
    string Artist,
    GlobalSystemMediaTransportControlsSession Session);

/// <summary>Обнаружение плееров через SMTC: имя приложения берём из системной сессии, а не из exe.</summary>
public static class Sources
{
    private static GlobalSystemMediaTransportControlsSessionManager? _manager;

    private static readonly (string Token, string Name)[] Known =
    {
        ("spotify", "Spotify"),
        ("yandex", "Яндекс Музыка"),
        ("ru.yandex", "Яндекс Музыка"),
        ("aimp", "AIMP"),
        ("foobar", "foobar2000"),
        ("winamp", "Winamp"),
        ("vlc", "VLC"),
        ("musicbee", "MusicBee"),
        ("itunes", "iTunes"),
        ("applemusic", "Apple Music"),
        ("deezer", "Deezer"),
        ("tidal", "TIDAL"),
        ("soundcloud", "SoundCloud"),
        ("wmplayer", "Windows Media Player"),
        ("music.ui", "Media Player"),
        ("zunemusic", "Groove Music"),
        ("youtube", "YouTube"),
    };

    public static async Task EnsureAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public static async Task<List<SmtcSession>> ListAsync()
    {
        var result = new List<SmtcSession>();
        try
        {
            await EnsureAsync();
            if (_manager == null) return result;
            foreach (var s in _manager.GetSessions())
            {
                string appId = s.SourceAppUserModelId ?? "";
                bool playing = s.GetPlaybackInfo()?.PlaybackStatus ==
                               GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                string title = "", artist = "";
                try
                {
                    var props = await s.TryGetMediaPropertiesAsync();
                    if (props != null) { title = props.Title ?? ""; artist = props.Artist ?? ""; }
                }
                catch { }
                result.Add(new SmtcSession(appId, NameFor(appId), playing, title, artist, s));
            }
        }
        catch { }
        return result;
    }

    public static string NameFor(string appId)
    {
        string low = appId.ToLowerInvariant();
        foreach (var (token, name) in Known)
            if (low.Contains(token)) return name;
        // fallback: имя exe/пакета без мусора
        var file = low.Replace(".exe", "");
        int slash = file.LastIndexOfAny(new[] { '\\', '/', '!' });
        if (slash >= 0) file = file[(slash + 1)..];
        return string.IsNullOrWhiteSpace(file) ? appId : file;
    }

    public static bool IsMusicApp(string appId)
    {
        string low = appId.ToLowerInvariant();
        foreach (var (token, _) in Known)
            if (low.Contains(token)) return true;
        return false;
    }
}
