using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GtaRadio;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _ni;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _active;
    private readonly AudioEngine _engine = new();
    private readonly ModPipe _pipe = new();
    private readonly AppConfig _cfg;
    private readonly DateTime _started = DateTime.Now;

    private SmtcSession? _current;
    private StatusForm? _status;
    private string? _lastAppId;
    private string? _autoPlayApp;
    private bool _prevGame;
    private bool _gameSeen;
    private bool _busy;
    private bool _duck;
    private bool _shutdown;

    public TrayApp(bool openStatus = false)
    {
        _cfg = AppConfig.Load();
        _engine.Volume = _cfg.Volume;
        _engine.DuckVolume = _cfg.DuckVolume;
        _engine.DeviceMatch = string.IsNullOrWhiteSpace(_cfg.OutputDevice) ? null : _cfg.OutputDevice;

        _ni = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "GTA Radio",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _ni.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        _ni.DoubleClick += (_, _) => OpenStatus();

        _pipe.StatusFactory = BuildStatus;
        _pipe.Command += OnCommand;
        _pipe.Start();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        _ = TickAsync();

        if (openStatus)
        {
            var once = new System.Windows.Forms.Timer { Interval = 600 };
            once.Tick += (_, _) => { once.Stop(); once.Dispose(); OpenStatus(); };
            once.Start();
        }
    }

    private void ToggleEnabled() => SetEnabled(!_cfg.Enabled);

    public void SetEnabled(bool value)
    {
        if (_cfg.Enabled == value) return;
        _cfg.Enabled = value;
        _cfg.Save();
        if (!value) { _engine.Stop(); _duck = false; }
        UpdateTooltip();
    }

    public void SetVolume(float value)
    {
        _cfg.Volume = value;
        _cfg.Save();
        _engine.Volume = value;
        _engine.ApplyVolume();
    }

    public RadioStatus Snapshot() => new(
        _gameSeen,
        _cfg.Enabled,
        _engine.IsRunning,
        _current?.Name,
        _current?.Title,
        _current?.Artist,
        _current?.Playing ?? false);

    public float Volume => _cfg.Volume;

    private void OpenStatus()
    {
        if (_status == null || _status.IsDisposed) _status = new StatusForm(this);
        _status.Show();
        _status.BringToFront();
        _status.Activate();
    }

    public void LaunchPlayer(string which)
    {
        try
        {
            if (which == "spotify")
            {
                string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Spotify\Spotify.exe");
                if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                else Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
            }
            else
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string exe = Path.Combine(local, @"Programs\YandexMusic\Яндекс Музыка.exe");
                if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                else Notify("Яндекс Музыка не найдена — запусти вручную");
            }
        }
        catch { Notify("Не удалось запустить плеер"); }
    }

    private void RebuildMenu()
    {
        var menu = _ni.ContextMenuStrip!;
        menu.Items.Clear();

        var status = new ToolStripMenuItem("Статус…");
        status.Font = new Font(status.Font, FontStyle.Bold);
        status.Click += (_, _) => OpenStatus();
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());

        var toggle = new ToolStripMenuItem(_cfg.Enabled ? "GTA Radio: включено" : "GTA Radio: выключено")
        { Checked = _cfg.Enabled, CheckOnClick = false };
        toggle.Click += (_, _) => ToggleEnabled();
        menu.Items.Add(toggle);
        menu.Items.Add(new ToolStripSeparator());

        var src = new ToolStripMenuItem("Источник");
        var auto = new ToolStripMenuItem("Авто (любой играющий плеер)") { Checked = _cfg.SourceAppId == null };
        auto.Click += (_, _) => { _cfg.SourceAppId = null; _cfg.Save(); RestartEngine(); };
        src.DropDownItems.Add(auto);
        src.DropDownItems.Add(new ToolStripSeparator());
        foreach (var s in _sessions)
        {
            var item = new ToolStripMenuItem(s.Name + (string.IsNullOrEmpty(s.Title) ? "" : " — " + s.Title))
            { Checked = _cfg.SourceAppId == s.AppId };
            var appId = s.AppId;
            item.Click += (_, _) => { _cfg.SourceAppId = appId; _cfg.Save(); RestartEngine(); };
            src.DropDownItems.Add(item);
        }
        if (_sessions.Count == 0) src.DropDownItems.Add(new ToolStripMenuItem("нет активных плееров") { Enabled = false });
        src.DropDownItems.Add(new ToolStripSeparator());
        var anyApp = new ToolStripMenuItem("Браузер и другие приложения (YouTube и т.п.)") { Checked = _cfg.AllowAnyApp };
        anyApp.Click += (_, _) => { _cfg.AllowAnyApp = !_cfg.AllowAnyApp; _cfg.Save(); RestartEngine(); };
        src.DropDownItems.Add(anyApp);
        menu.Items.Add(src);

        var vol = new ToolStripMenuItem("Громкость");
        foreach (var v in new[] { 0.25f, 0.5f, 0.75f, 1.0f })
        {
            var item = new ToolStripMenuItem(((int)(v * 100)) + "%") { Checked = Math.Abs(_cfg.Volume - v) < 0.01f };
            float vv = v;
            item.Click += (_, _) => { _cfg.Volume = vv; _cfg.Save(); _engine.Volume = vv; _engine.ApplyVolume(); };
            vol.DropDownItems.Add(item);
        }
        menu.Items.Add(vol);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Удалить мод из игры", null, (_, _) => Uninstall()));
        menu.Items.Add(new ToolStripMenuItem("Открыть папку игры", null, (_, _) => OpenGameFolder()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Выход", null, (_, _) => Shutdown()));
    }

    private List<SmtcSession> _sessions = new();

    private void Uninstall()
    {
        string? dir = _cfg.GameDir ?? Installer.FindGame();
        if (dir == null) { Notify("Папка игры не найдена"); return; }
        if (MessageBox.Show("Удалить мод GTA Radio из игры? Файлы игры и перевод не затрагиваются.",
                "GTA Radio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Installer.Uninstall(dir);
        Notify("Мод удалён. Бэкап остался в папке игры.");
    }

    private void OpenGameFolder()
    {
        string? dir = _cfg.GameDir ?? Installer.FindGame();
        if (dir == null) { Notify("Папка игры не найдена"); return; }
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true }); } catch { }
    }

    private async Task TickAsync()
    {
        if (_busy || _shutdown) return;
        _busy = true;
        try
        {
            var game = GameWatcher.Find(out var gamePath);
            if (game != null)
            {
                _gameSeen = true;
                if (string.IsNullOrEmpty(_cfg.GameDir) && gamePath != null)
                {
                    _cfg.GameDir = Path.GetDirectoryName(gamePath);
                    _cfg.Save();
                }
            }
            else if (_gameSeen && _cfg.ExitWithGame)
            {
                Shutdown();
                return;
            }
            else if (!_gameSeen && (DateTime.Now - _started).TotalSeconds > _cfg.WaitForGameSeconds)
            {
                Shutdown();
                return;
            }

            _sessions = await Sources.ListAsync();
            var chosen = Choose(_sessions);
            _current = chosen;

            if (game != null && !_prevGame) Notify("GTA V запущена — ищу плеер");
            _prevGame = game != null;
            if (chosen?.AppId != _lastAppId)
            {
                if (chosen != null) Notify("Подключено: " + chosen.Name);
                else if (_gameSeen) Notify("Плеер не найден — включи музыку в Spotify или Яндекс Музыке");
                _lastAppId = chosen?.AppId;
            }

            Log($"tick game={(game != null)} sessions={_sessions.Count} chosen={chosen?.AppId ?? "-"} run={_engine.IsRunning} lvl={_engine.Level:F0}");

            if (game != null && chosen != null && !chosen.Playing && _autoPlayApp != chosen.AppId)
            {
                _autoPlayApp = chosen.AppId;
                try { await chosen.Session.TryPlayAsync(); } catch { }
            }

            if (!_cfg.Enabled || game == null || chosen == null)
            {
                _active = false;
                if (_engine.IsRunning) _engine.Stop();
            }
            else
            {
                _active = true;
                if (!_cfg.ControlOnly && chosen.Playing)
                {
                    var pids = ResolvePids(chosen);
                    uint main = MainPid(pids);
                    if (main != 0 && (!_engine.IsRunning || _engine.CurrentPid != main))
                    {
                        _engine.Stop();
                        var ok = _engine.Start(main, pids, chosen.AppId, (uint)game.Id);
                        Log($"start {chosen.Name} pid={main} game={game.Id} ok={ok} out={_engine.OutputName}");
                    }
                }
            }

            if (_cfg.ControlOnly && _engine.IsRunning) _engine.Stop();
            _engine.SetDuck(_duck);
            UpdateHeartbeat();
            WriteState(chosen);
            UpdateTooltip();
        }
        catch (Exception ex) { Log("ERR " + ex); }
        finally { _busy = false; }
    }

    private void WriteState(SmtcSession? s)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
            Directory.CreateDirectory(dir);
            static string Clean(string? v) => (v ?? "").Replace("\r", " ").Replace("\n", " ").Replace("=", "-");
            var text = "active=" + (_active ? "1" : "0") + "\n" +
                       "app=" + Clean(s?.Name) + "\n" +
                       "artist=" + Clean(s?.Artist) + "\n" +
                       "title=" + Clean(s?.Title) + "\n" +
                       "playing=" + (s?.Playing == true ? "1" : "0") + "\n";
            File.WriteAllText(Path.Combine(dir, "state.txt"), text);
        }
        catch { }
    }

    private void UpdateHeartbeat()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "engine.on");
            if (_active) File.WriteAllText(file, DateTime.UtcNow.Ticks.ToString());
            else if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }

    private static void Log(string msg)
    {
        try
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "agent.log"), DateTime.Now.ToString("HH:mm:ss ") + msg + Environment.NewLine);
        }
        catch { }
    }

    private SmtcSession? Choose(List<SmtcSession> list)
    {
        if (list.Count == 0) return null;
        var music = list.Where(s => Sources.IsMusicApp(s.AppId)).ToList();

        // Гистерезис: держим текущий источник, пока он жив и никто другой не играет.
        if (_engine.IsRunning && _engine.CurrentAppId != null)
        {
            var cur = list.FirstOrDefault(s => s.AppId == _engine.CurrentAppId);
            if (cur != null && (cur.Playing || !list.Any(s => s.Playing && s.AppId != cur.AppId)))
                return cur;
        }

        if (_cfg.SourceAppId != null)
        {
            var exact = list.FirstOrDefault(s => s.AppId.Contains(_cfg.SourceAppId, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }
        if (music.Count > 0)
        {
            var playing = music.FirstOrDefault(s => s.Playing);
            return playing ?? music[0];
        }
        if (_cfg.AllowAnyApp)
            return list.FirstOrDefault(s => s.Playing);
        return null;
    }

    private static HashSet<uint> ResolvePids(SmtcSession s)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in s.AppId.Split(new[] { '\\', '/', '!', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
            if (part.Length > 2) tokens.Add(part);
        foreach (var word in s.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (word.Length > 2) tokens.Add(word);

        var result = new HashSet<uint>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                if (tokens.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    result.Add((uint)p.Id);
            }
            catch { }
        }
        return result;
    }

    private static uint MainPid(HashSet<uint> pids)
    {
        if (pids.Count == 0) return 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (pids.Contains((uint)p.Id) && p.MainWindowHandle != IntPtr.Zero) return (uint)p.Id;
            }
            catch { }
        }
        return pids.First();
    }

    private void RestartEngine()
    {
        _engine.Stop();
        _ = TickAsync();
    }

    private string BuildStatus()
    {
        bool active = _cfg.Enabled && _engine.IsRunning;
        string app = _current?.Name ?? "";
        string title = _current?.Title ?? "";
        string artist = _current?.Artist ?? "";
        bool playing = _current?.Playing ?? false;
        return $"active={(active ? 1 : 0)}|app={Clean(app)}|title={Clean(title)}|artist={Clean(artist)}|playing={(playing ? 1 : 0)}";
    }

    private static string Clean(string s) => s.Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim();

    private void OnCommand(string cmd)
    {
        switch (cmd.Trim().ToLowerInvariant())
        {
            case "toggle": ToggleEnabled(); break;
            case "playpause": _ = _current?.Session.TryTogglePlayPauseAsync(); break;
            case "next": _ = _current?.Session.TrySkipNextAsync(); break;
            case "prev": _ = _current?.Session.TrySkipPreviousAsync(); break;
            case "duck 0": _duck = true; _engine.SetDuck(true); break;
            case "duck 1": _duck = false; _engine.SetDuck(false); break;
            case "hello": break;
        }
    }

    private void UpdateTooltip()
    {
        string text;
        if (!_cfg.Enabled) text = "GTA Radio — выключено";
        else if (_current == null) text = "GTA Radio — жду плеер (Spotify/Яндекс)";
        else text = _current.Name + (_current.Playing ? " — играет" : " — пауза") +
                    (string.IsNullOrEmpty(_current.Title) ? "" : "\n" + _current.Artist + " — " + _current.Title);
        try { _ni.Text = text.Length > 63 ? text[..63] : text; } catch { }
    }

    private void Notify(string text)
    {
        try { _ni.ShowBalloonTip(2500, "GTA Radio", text, ToolTipIcon.Info); } catch { }
    }

    private void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        try { _timer.Stop(); } catch { }
        try { _engine.Dispose(); } catch { }
        try { _pipe.Dispose(); } catch { }
        try { _ni.Visible = false; _ni.Dispose(); } catch { }
        ExitThread();
    }
}
