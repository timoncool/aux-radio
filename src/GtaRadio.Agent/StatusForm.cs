using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GtaRadio.Agent;

/// <summary>Окно статуса агента: игра, режим, плеер, сессии, горячие клавиши и ссылки. RU/EN.</summary>
internal sealed class StatusForm : Form
{
    private static readonly Color Bg = Color.FromArgb(24, 24, 28);
    private static readonly Color Panel = Color.FromArgb(34, 34, 40);
    private static readonly Color Fg = Color.FromArgb(235, 235, 240);
    private static readonly Color Dim = Color.FromArgb(160, 160, 170);
    private static readonly Color Accent = Color.FromArgb(245, 197, 24);

    private readonly AgentApp _app;
    private readonly Label _title;
    private readonly Label _sub;
    private readonly Label _status;
    private readonly Label _modeCap, _playerCap, _trackCap;
    private readonly Label _mode, _player, _track;
    private readonly Label _sesTitle, _keys, _linksTitle;
    private readonly ListView _sessions;
    private readonly Button _logs, _close, _langBtn;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _en;

    public StatusForm(AgentApp app)
    {
        _app = app;
        _en = string.Equals(app.Config.Language, "en", StringComparison.OrdinalIgnoreCase);

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        ClientSize = new Size(620, 680);
        MinimumSize = new Size(540, 620);
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        MaximizeBox = false;

        var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (System.IO.File.Exists(logoPath))
        {
            Controls.Add(new PictureBox
            {
                Image = Image.FromFile(logoPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(24, 18),
                Size = new Size(84, 84)
            });
        }

        _title = new Label
        {
            Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
            ForeColor = Accent,
            AutoSize = true,
            Location = new Point(122, 20)
        };
        Controls.Add(_title);

        _sub = new Label
        {
            ForeColor = Dim,
            AutoSize = true,
            Location = new Point(124, 66)
        };
        Controls.Add(_sub);

        _langBtn = new Button
        {
            Size = new Size(72, 30),
            Location = new Point(ClientSize.Width - 96, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Fg,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _langBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _langBtn.Click += (_, _) =>
        {
            _en = !_en;
            _app.Config.Language = _en ? "en" : "ru";
            _app.Config.Save();
            ApplyLanguage();
        };
        Controls.Add(_langBtn);

        _status = new Label
        {
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            ForeColor = Fg,
            AutoSize = false,
            Location = new Point(24, 112),
            Size = new Size(572, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_status);

        var info = new Panel
        {
            Location = new Point(24, 152),
            Size = new Size(572, 112),
            BackColor = Panel,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(info);
        _mode = AddRow(info, out _modeCap, 10);
        _player = AddRow(info, out _playerCap, 46);
        _track = AddRow(info, out _trackCap, 82);

        _sesTitle = new Label { ForeColor = Dim, AutoSize = true, Location = new Point(24, 276) };
        Controls.Add(_sesTitle);

        _sessions = new ListView
        {
            Location = new Point(24, 300),
            Size = new Size(572, 150),
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = Panel,
            ForeColor = Fg,
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _sessions.Columns.Add("", 150);
        _sessions.Columns.Add("", 340);
        _sessions.Columns.Add("", 70);
        Controls.Add(_sessions);

        _keys = new Label
        {
            ForeColor = Fg,
            AutoSize = false,
            Location = new Point(24, 464),
            Size = new Size(572, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_keys);

        _linksTitle = new Label { ForeColor = Dim, AutoSize = true, Location = new Point(24, 522) };
        Controls.Add(_linksTitle);

        int ly = 546;
        AddLink(_app.Config.GithubUrl, "GitHub", ref ly);
        if (!string.IsNullOrWhiteSpace(_app.Config.BoostyUrl)) AddLink(_app.Config.BoostyUrl, "Boosty", ref ly);
        if (!string.IsNullOrWhiteSpace(_app.Config.TelegramUrl)) AddLink(_app.Config.TelegramUrl, "Telegram", ref ly);

        _logs = new Button
        {
            Location = new Point(24, 628),
            Size = new Size(150, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Fg,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _logs.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _logs.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Log.Dir + "\"") { UseShellExecute = true }); } catch { }
        };
        Controls.Add(_logs);

        _close = new Button
        {
            Location = new Point(446, 628),
            Size = new Size(150, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Panel,
            ForeColor = Fg,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _close.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
        _close.Click += (_, _) => Close();
        Controls.Add(_close);

        ApplyLanguage();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => RefreshState();
        _timer.Start();
        RefreshState();
    }

    private static Label AddRow(Panel p, out Label caption, int y)
    {
        caption = new Label { ForeColor = Dim, AutoSize = true, Location = new Point(12, y + 3) };
        p.Controls.Add(caption);
        var val = new Label
        {
            Text = "—",
            ForeColor = Fg,
            AutoSize = false,
            Location = new Point(120, y),
            Size = new Size(440, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        p.Controls.Add(val);
        return val;
    }

    private void AddLink(string url, string caption, ref int y)
    {
        var l = new LinkLabel
        {
            Text = caption,
            LinkColor = Color.FromArgb(120, 190, 255),
            ActiveLinkColor = Accent,
            VisitedLinkColor = Color.FromArgb(120, 190, 255),
            AutoSize = true,
            Location = new Point(24, y)
        };
        l.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };
        Controls.Add(l);
        y += 26;
    }

    private void ApplyLanguage()
    {
        Text = _en ? "AUX Radio — status" : "AUX Radio — статус";
        _title.Text = "AUX Radio";
        _sub.Text = _en ? "your own player instead of Non-Stop-Pop" : "внешний плеер вместо радиостанции Non-Stop-Pop";
        _langBtn.Text = _en ? "РУ" : "EN";
        _modeCap.Text = _en ? "Mode" : "Режим";
        _playerCap.Text = _en ? "Player" : "Плеер";
        _trackCap.Text = _en ? "Track" : "Трек";
        _sesTitle.Text = _en ? "Windows media sessions" : "Медиасессии Windows";
        _sessions.Columns[0].Text = _en ? "Player" : "Плеер";
        _sessions.Columns[1].Text = _en ? "Track" : "Трек";
        _sessions.Columns[2].Text = _en ? "State" : "Статус";
        _keys.Text = _en
            ? "Hotkeys (in a car on AUX)\r\nF7 — play / pause     F8 — next track     F6 — previous"
            : "Горячие клавиши (в машине на AUX)\r\nF7 — пауза / плей     F8 — следующий трек     F6 — предыдущий";
        _linksTitle.Text = _en ? "Links" : "Ссылки";
        _logs.Text = _en ? "Open logs" : "Открыть логи";
        _close.Text = _en ? "Close" : "Закрыть";
        RefreshState();
    }

    private string Tr(string s)
    {
        if (!_en) return s;
        if (s.StartsWith("В эфире: ")) return "On air: " + s.Substring(9);
        if (s.StartsWith("Готов: ")) return "Ready: " + s.Substring(7);
        if (s == "GTA V (пауза)") return "GTA V (paused)";
        if (s == "Жду GTA V") return "Waiting for GTA V";
        if (s == "Игра не отвечает") return "Game is not responding";
        if (s == "Плеер не найден") return "Player not found";
        return s;
    }

    private void RefreshState()
    {
        _status.Text = Tr(_app.Status);
        _status.ForeColor = _app.GameRunning ? Color.FromArgb(120, 220, 140) : Color.FromArgb(240, 190, 90);

        _mode.Text = _app.Link?.Output switch
        {
            0 => _en ? "Game — the game plays the sound" : "Game — звук играет игра",
            1 => _en ? "Agent — the agent plays the sound" : "Agent — звук играет агент",
            2 => _en ? "Remote — the player plays by itself" : "Remote — плеер играет сам",
            _ => "—"
        };
        _player.Text = _app.Current?.Name ?? (_en ? "not found" : "не найден");
        var c = _app.Current;
        _track.Text = c == null ? "—" : (string.IsNullOrEmpty(c.Artist) ? c.Title : c.Artist + " — " + c.Title);

        _sessions.BeginUpdate();
        _sessions.Items.Clear();
        foreach (var s in _app.Sessions)
        {
            var it = new ListViewItem(s.Name);
            it.SubItems.Add(string.IsNullOrEmpty(s.Artist) ? s.Title : s.Artist + " — " + s.Title);
            it.SubItems.Add(s.Playing ? (_en ? "playing" : "играет") : (_en ? "paused" : "пауза"));
            it.ForeColor = s.AppId == _app.Current?.AppId ? Accent : Fg;
            _sessions.Items.Add(it);
        }
        _sessions.EndUpdate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        base.OnFormClosed(e);
    }
}
