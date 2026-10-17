using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using GTA;
using GTA.Native;
using GTA.UI;

namespace GtaRadioMod
{
    /// <summary>
    /// GTA Radio: станция RADIO_02_POP работает как пульт для Яндекс.Музыки/Spotify.
    /// Звук плеера не захватывается и не перевыводится — играет сам плеер.
    /// Мод глушит штатную музыку станции, показывает трек и ставит плеер на паузу,
    /// когда игрок выходит из машины или переключает станцию.
    /// </summary>
    public class GtaRadioMod : Script
    {
        private const string RadioId = "RADIO_02_POP";
        private const byte VkMediaPlayPause = 0xB3;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static readonly string StateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GtaRadio", "state.txt");

        private readonly Dictionary<string, string> _state = new Dictionary<string, string>();
        private DateTime _lastPoll = DateTime.MinValue;
        private bool _radioOff;
        private bool _want;
        private bool _prevWant;
        private bool _prevPlaying;

        public GtaRadioMod()
        {
            Tick += OnTick;
            Aborted += (_, __) => SetRadio(true);
            Interval = 0;
        }

        private static void SetRadio(bool on)
        {
            try
            {
                var veh = Game.Player.Character.CurrentVehicle;
                if (veh != null) Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, veh, on);
            }
            catch { }
        }

        private static void SendPlayPause()
        {
            try
            {
                keybd_event(VkMediaPlayPause, 0, 0, UIntPtr.Zero);
                keybd_event(VkMediaPlayPause, 0, 2, UIntPtr.Zero);
            }
            catch { }
        }

        private void Poll()
        {
            _state.Clear();
            try
            {
                if (!File.Exists(StateFile)) return;
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(StateFile)).TotalSeconds > 4) return;
                foreach (var line in File.ReadAllLines(StateFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) _state[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch { }
        }

        private string Get(string key)
        {
            string v;
            return _state.TryGetValue(key, out v) ? v : "";
        }

        private bool Active { get { return Get("active") == "1"; } }
        private bool Playing { get { return Get("playing") == "1"; } }

        private static bool InOurStation()
        {
            try
            {
                var name = Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME);
                return string.Equals(name, RadioId, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if ((DateTime.UtcNow - _lastPoll).TotalMilliseconds < 350) return;
            _lastPoll = DateTime.UtcNow;

            Poll();

            var veh = Game.Player.Character.CurrentVehicle;
            bool wheel = false;
            try { wheel = Game.IsControlPressed(GTA.Control.VehicleRadioWheel); } catch { }
            _want = Active && veh != null && veh.IsEngineRunning && InOurStation();

            // Как нативное радио: вошли в машину/выбрали станцию — играем, вышли — пауза.
            if (_want && !_prevWant && !Playing) SendPlayPause();
            if (!_want && _prevWant && Playing) SendPlayPause();
            _prevWant = _want;

            if (_want && !wheel)
            {
                SetRadio(false);
                _radioOff = true;
            }
            else if (_radioOff)
            {
                SetRadio(true);
                _radioOff = false;
            }

            if (_want) DrawHud();
            _prevPlaying = Playing;
        }

        private void DrawHud()
        {
            string app = Get("app");
            string artist = Get("artist");
            string title = Get("title");
            bool playing = Get("playing") == "1";

            string line2 = string.IsNullOrEmpty(app) ? "Плеер" : app;
            string line3 = playing
                ? (string.IsNullOrEmpty(artist) ? title : artist + " — " + title)
                : "ПАУЗА";

            Function.Call(Hash.DRAW_RECT, 0.175f, 0.805f, 0.33f, 0.105f, 0, 0, 0, 170);

            new TextElement("GTA Radio", new PointF(0.030f, 0.770f), 0.46f, Color.White,
                GTA.UI.Font.ChaletLondon, Alignment.Left).Draw();
            new TextElement(line2, new PointF(0.030f, 0.800f), 0.34f, Color.FromArgb(200, 180, 220, 255),
                GTA.UI.Font.ChaletLondon, Alignment.Left).Draw();
            if (!string.IsNullOrEmpty(line3))
                new TextElement(line3, new PointF(0.030f, 0.830f), 0.34f, Color.FromArgb(220, 230, 230, 230),
                    GTA.UI.Font.ChaletLondon, Alignment.Left).Draw();
        }
    }
}
