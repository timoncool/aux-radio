using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GtaRadio;

public sealed record RadioStatus(
    bool GameRunning,
    bool Enabled,
    bool Connected,
    string? App,
    string? Title,
    string? Artist,
    bool Playing);

/// <summary>Окно статуса агента: игра, плеер, звук, трек, громкость, хоткеи.</summary>
public sealed class StatusForm : Form
{
    private readonly TrayApp _owner;
    private readonly System.Windows.Forms.Timer _timer;

    private readonly Label _gameValue = Value();
    private readonly Label _playerValue = Value();
    private readonly Label _soundValue = Value();
    private readonly Label _trackValue = new()
    {
        AutoSize = false, Height = 40, ForeColor = Color.FromArgb(220, 220, 226),
        Font = new Font("Segoe UI", 9.5f)
    };
    private readonly Button _toggle;
    private readonly TrackBar _volume;
    private readonly Label _hint = new()
    {
        AutoSize = false, Height = 36, ForeColor = Color.FromArgb(150, 150, 158),
        Font = new Font("Segoe UI", 8.5f)
    };

    public StatusForm(TrayApp owner)
    {
        _owner = owner;
        Text = "GTA Radio — статус";
        ClientSize = new Size(520, 500);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(14, 14, 16);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var head = new Label
        {
            Text = "GTA RADIO", Font = new Font("Segoe UI Semibold", 18f),
            ForeColor = Color.FromArgb(245, 197, 24), Bounds = new Rectangle(24, 18, 300, 32)
        };
        Controls.Add(head);
        Controls.Add(new Label
        {
            Text = "Spotify и Яндекс Музыка — как радиостанция в GTA V",
            ForeColor = Color.FromArgb(160, 160, 168), Bounds = new Rectangle(24, 50, 420, 20)
        });

        int y = 88;
        Row("Игра", _gameValue, ref y);
        Row("Плеер", _playerValue, ref y);
        Row("Звук", _soundValue, ref y);

        _trackValue.Bounds = new Rectangle(24, y + 4, 470, 44);
        _trackValue.AutoEllipsis = true;
        Controls.Add(_trackValue);

        _toggle = new Button
        {
            Text = "Выключить", FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(245, 197, 24), ForeColor = Color.Black,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Bounds = new Rectangle(24, y + 56, 150, 34)
        };
        _toggle.FlatAppearance.BorderSize = 0;
        _toggle.Click += (_, _) => { _owner.SetEnabled(!_owner.Snapshot().Enabled); Refresh2(); };
        Controls.Add(_toggle);

        var spotify = SmallButton("Открыть Spotify", 24, y + 100);
        spotify.Click += (_, _) => _owner.LaunchPlayer("spotify");
        var yandex = SmallButton("Открыть Яндекс Музыку", 180, y + 100);
        yandex.Bounds = new Rectangle(180, y + 100, 180, 32);
        yandex.Click += (_, _) => _owner.LaunchPlayer("yandex");

        _hint.Bounds = new Rectangle(24, y + 146, 470, 44);
        _hint.Text = "Хоткеи в игре: F7 — вкл/выкл · F8 — предыдущий · F9 — пауза · F10 — следующий\n" +
                     "Окно можно закрыть — агент остаётся в трее и закрывается вместе с игрой.";
        Controls.Add(_hint);

        _volume = new TrackBar
        {
            Minimum = 0, Maximum = 100, TickStyle = TickStyle.None,
            Bounds = new Rectangle(24, y + 198, 340, 32),
            Value = Math.Clamp((int)(_owner.Volume * 100), 0, 100)
        };
        _volume.ValueChanged += (_, _) => _owner.SetVolume(_volume.Value / 100f);
        Controls.Add(_volume);
        Controls.Add(new Label
        {
            Text = "Громкость", ForeColor = Color.FromArgb(160, 160, 168),
            Bounds = new Rectangle(378, y + 202, 110, 20)
        });

        var close = SmallButton("Скрыть", 24, y + 240);
        close.Click += (_, _) => Hide();

        _timer = new System.Windows.Forms.Timer { Interval = 700 };
        _timer.Tick += (_, _) => Refresh2();
        _timer.Start();
        Refresh2();

        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        Paint += PaintHero;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, sizeof(int)); } catch { }
    }

    private void Row(string caption, Label value, ref int y)
    {
        Controls.Add(new Label
        {
            Text = caption, ForeColor = Color.FromArgb(150, 150, 158),
            Bounds = new Rectangle(24, y, 90, 22)
        });
        value.Bounds = new Rectangle(120, y, 330, 22);
        Controls.Add(value);
        y += 30;
    }

    private Button SmallButton(string text, int x, int y)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(38, 38, 44), ForeColor = Color.White,
            Bounds = new Rectangle(x, y, 140, 32)
        };
        b.FlatAppearance.BorderSize = 0;
        Controls.Add(b);
        return b;
    }

    private static Label Value() => new()
    {
        AutoSize = false, Height = 22, ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.5f)
    };

    private void Refresh2()
    {
        var s = _owner.Snapshot();
        _gameValue.Text = s.GameRunning ? "GTA V запущена" : "GTA V не запущена — жду игру";
        _gameValue.ForeColor = s.GameRunning ? Color.FromArgb(87, 209, 107) : Color.FromArgb(200, 200, 206);

        if (s.App == null)
        {
            _playerValue.Text = "Плеер не найден — включи музыку";
            _playerValue.ForeColor = Color.FromArgb(245, 197, 24);
        }
        else
        {
            _playerValue.Text = s.App + (s.Playing ? " — играет" : " — пауза");
            _playerValue.ForeColor = s.Playing ? Color.FromArgb(87, 209, 107) : Color.FromArgb(200, 200, 206);
        }

        _soundValue.Text = !s.Enabled ? "Выключено"
            : s.Connected ? "Захвачен и играет в игре"
            : "Ожидание";
        _soundValue.ForeColor = s.Connected ? Color.FromArgb(87, 209, 107) : Color.FromArgb(200, 200, 206);

        _trackValue.Text = s.App == null
            ? "Запусти Spotify или Яндекс Музыку и нажми play — агент подхватит автоматически."
            : (string.IsNullOrEmpty(s.Artist) ? s.Title : s.Artist + " — " + s.Title);

        _toggle.Text = s.Enabled ? "Выключить" : "Включить";
    }

    private void PaintHero(object? sender, PaintEventArgs e)
    {
        var r = new Rectangle(0, 0, ClientSize.Width, 6);
        using var b = new LinearGradientBrush(r, Color.FromArgb(245, 197, 24), Color.FromArgb(120, 96, 10), 0f);
        e.Graphics.FillRectangle(b, r);
    }
}
