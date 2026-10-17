using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using GtaRadio.Shared;

namespace GtaRadio.Agent;

internal sealed class AgentConfig
{
    public string? Source { get; set; }              // null = авто; иначе часть AppId/имени ("spotify", "yandex", ...)
    public float Attenuation { get; set; } = 0.001f; // громкость плеера в микшере во время эфира (-60 дБ)
    public int PauseDelayMs { get; set; } = 1200;    // как у MichonGoddijn: короткий выход со станции не ставит паузу
    public bool PauseWhenOffAir { get; set; } = true;

    private static string PathJson => System.IO.Path.Combine(Log.Dir, "agent.json");

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(PathJson))
                return JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(PathJson)) ?? new AgentConfig();
        }
        catch { }
        var c = new AgentConfig();
        c.Save();
        return c;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.Dir);
            File.WriteAllText(PathJson, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

/// <summary>
/// Агент в трее. Ждёт игру (общая память RadioLink создаётся модом), пока игрок на GTA Radio —
/// отдаёт звук выбранного плеера в игру, вне эфира ставит плеер на паузу и возвращает ему громкость.
/// Закрывается вместе с игрой.
/// </summary>
internal sealed class AgentApp : ApplicationContext
{
    private readonly AgentConfig _cfg = AgentConfig.Load();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _fast = new() { Interval = 50 };
    private readonly System.Windows.Forms.Timer _slow = new() { Interval = 1000 };

    private RadioLink? _link;
    private Streamer? _streamer;
    private List<PlayerSession> _sessions = new();
    private PlayerSession? _current;
    private bool _owned;
    private long _offAirSince;
    private long _stopAt;
    private bool _pausedByUs;
    private int _lastCmdSeq = -1;
    private bool _gameSeen;
    private long _gameLostAt;
    private bool _busy;
    private string _metaKey = "";
    private readonly long _started = Environment.TickCount64;

    public AgentApp()
    {
        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "GTA Radio",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _fast.Tick += (_, _) => FastTick();
        _slow.Tick += async (_, _) => await SlowTick();
        _fast.Start();
        _slow.Start();
        Log.W("agent start");
    }

    private static uint GamePid()
    {
        foreach (var name in new[] { "GTA5_Enhanced", "GTA5" })
        {
            var list = Process.GetProcessesByName(name);
            try { if (list.Length > 0) return (uint)list[0].Id; }
            finally { foreach (var p in list) p.Dispose(); }
        }
        return 0;
    }

    private void FastTick()
    {
        if (_link == null) return;
        long now = Environment.TickCount64;
        bool alive = _link.GameAlive;
        bool owned = alive && _link.Owned;

        if (owned && !_owned) OnAir();
        if (!owned && _owned) _offAirSince = now;
        _owned = owned;

        // Вне эфира дольше задержки: пауза, затем (после затухания плеера) вернуть ему громкость.
        if (!_owned && _offAirSince != 0 && now - _offAirSince >= _cfg.PauseDelayMs)
        {
            _offAirSince = 0;
            if (_cfg.PauseWhenOffAir && _current != null && _current.Playing)
            {
                _ = _current.Session.TryPauseAsync().AsTask();
                _pausedByUs = true;
            }
            _stopAt = now + 400;
        }
        if (_stopAt != 0 && now >= _stopAt)
        {
            _stopAt = 0;
            if (!_owned) _streamer?.Stop();
        }

        // Команды из игры.
        int seq = _link.CmdSeq;
        if (_lastCmdSeq < 0) _lastCmdSeq = seq;
        if (seq != _lastCmdSeq)
        {
            _lastCmdSeq = seq;
            var s = _current?.Session;
            if (s != null)
            {
                switch (_link.Cmd)
                {
                    case RadioLink.CmdPlayPause: _ = s.TryTogglePlayPauseAsync().AsTask(); break;
                    case RadioLink.CmdNext: _ = s.TrySkipNextAsync().AsTask(); break;
                    case RadioLink.CmdPrev: _ = s.TrySkipPreviousAsync().AsTask(); break;
                }
            }
        }

        _link.AgentBeat(_streamer?.Running == true ? RadioLink.StateStreaming
                        : _current != null ? RadioLink.StateReady : RadioLink.StateNone,
                        _current?.Playing == true);
    }

    private void OnAir()
    {
        _offAirSince = 0;
        _stopAt = 0;
        if (_current == null || _link == null) return;
        if (!_current.Playing && (_pausedByUs || _cfg.PauseWhenOffAir))
            _ = _current.Session.TryPlayAsync().AsTask();
        _pausedByUs = false;
        EnsureStreaming();
    }

    private void EnsureStreaming()
    {
        if (_link == null || _current == null) return;
        _streamer ??= new Streamer(_link, _cfg.Attenuation);
        uint game = GamePid();
        uint root = ProcessTree.FindPlayerRoot(_current.AppId, game, SessionVolumes.LoudPids());
        if (root == 0) { Log.W("[ERROR] player process not found for " + _current.AppId); return; }
        if (_streamer.Running && _streamer.RootPid == root) return;
        if (!_streamer.Start(root, game)) Notify("Не удалось захватить звук: " + _current.Name);
    }

    private async Task SlowTick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (_link == null)
            {
                _link = RadioLink.TryOpen();
                if (_link != null) { _gameSeen = true; Log.W("game link open, rate=" + _link.SampleRate); }
            }

            // Жизненный цикл: агент живёт, пока жива игра.
            // Процесса игры нет -> выход через ~3 с; мод молчит (загрузка) -> ждём, пока процесс есть.
            // Запущен без игры -> ждём игру до 15 минут.
            if (_link != null && !_link.GameAlive)
            {
                if (_gameLostAt == 0) _gameLostAt = Environment.TickCount64;
                long lost = Environment.TickCount64 - _gameLostAt;
                if (lost > 3000 && GamePid() == 0) { Shutdown(false); return; }
            }
            else _gameLostAt = 0;
            if (!_gameSeen && Environment.TickCount64 - _started > 15 * 60 * 1000) { Shutdown(false); return; }

            _sessions = await Smtc.ListAsync();
            var chosen = Smtc.Choose(_sessions, _cfg.Source, _current?.AppId);
            if (chosen?.AppId != _current?.AppId && _streamer?.Running == true) _streamer.Stop();
            _current = chosen;

            if (_owned) { EnsureStreaming(); _streamer?.Maintain(); }

            if (_link != null && _current != null)
            {
                string key = _current.Name + "|" + _current.Artist + "|" + _current.Title;
                if (key != _metaKey)
                {
                    _metaKey = key;
                    _link.WriteMeta(_current.Name, _current.Artist, _current.Title);
                }
            }
            UpdateTooltip();
        }
        catch (Exception ex) { Log.W("[ERROR] tick " + ex); }
        finally { _busy = false; }
    }

    private void BuildMenu()
    {
        var m = _tray.ContextMenuStrip!;
        m.Items.Clear();
        m.Items.Add(new ToolStripMenuItem(StatusText()) { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        var src = new ToolStripMenuItem("Источник");
        var auto = new ToolStripMenuItem("Авто (играющий плеер)") { Checked = _cfg.Source == null };
        auto.Click += (_, _) => { _cfg.Source = null; _cfg.Save(); };
        src.DropDownItems.Add(auto);
        foreach (var s in _sessions)
        {
            var item = new ToolStripMenuItem(s.Name + (s.Title.Length > 0 ? " — " + s.Title : ""))
            { Checked = _cfg.Source != null && s.AppId.Contains(_cfg.Source, StringComparison.OrdinalIgnoreCase) };
            var id = s.AppId;
            item.Click += (_, _) => { _cfg.Source = id; _cfg.Save(); };
            src.DropDownItems.Add(item);
        }
        m.Items.Add(src);
        m.Items.Add(new ToolStripMenuItem("Папка логов", null, (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Log.Dir + "\"") { UseShellExecute = true }); } catch { }
        }));
        m.Items.Add(new ToolStripSeparator());
        // Ручной выход: мод не перезапускает агент до конца этой игровой сессии.
        m.Items.Add(new ToolStripMenuItem("Выключить GTA Radio до перезапуска игры", null, (_, _) => Shutdown(true)));
    }

    private string StatusText()
    {
        if (_link == null || !_link.GameAlive) return _gameSeen ? "Игра не отвечает" : "Жду GTA V";
        if (_current == null) return "Плеер не найден";
        if (_streamer?.Running == true) return "В эфире: " + _current.Name;
        return "Готов: " + _current.Name;
    }

    private void UpdateTooltip()
    {
        string t = "GTA Radio — " + StatusText();
        try { _tray.Text = t.Length > 63 ? t[..63] : t; } catch { }
    }

    private void Notify(string text)
    {
        try { _tray.ShowBalloonTip(2500, "GTA Radio", text, ToolTipIcon.Info); } catch { }
    }

    private void Shutdown(bool byUser)
    {
        Log.W("agent exit" + (byUser ? " (user)" : ""));
        _fast.Stop();
        _slow.Stop();
        try { _streamer?.Stop(); } catch { }
        SessionVolumes.RestoreAll();
        try { if (byUser) _link?.AgentBeat(RadioLink.StateUserExit, false); } catch { }
        try { _link?.Dispose(); } catch { }
        try { _tray.Visible = false; _tray.Dispose(); } catch { }
        ExitThread();
    }
}
