using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GtaRadio.Shared;

namespace GtaRadio.Mod
{
    /// <summary>
    /// GTA Radio: станция RADIO_02_POP (в колесе — «GTA Radio», имя из GXT2) играет поток агента:
    /// звук Яндекс Музыки, Spotify или любого другого плеера.
    ///
    /// Схема — как у рабочего Enhanced-мода MichonGoddijn231849/gta-spotify-radio:
    /// - станция наша, если выбрана RADIO_02_POP, или мы сами держим радио в OFF (station_ownership.cpp);
    /// - во время перестройки (IS_RADIO_RETUNING) решение не меняется и глушение не применяется;
    /// - штатная музыка глушится аудио-сценой + заморозкой станции + радио машины в OFF,
    ///   OFF переутверждается каждый кадр (игра сама включает радио при смене машины);
    /// - громкость следует игре: ползунок музыки, пауза, катсцены, разговоры, салон, вода.
    /// Трек — в штатных местах игры: лента уведомлений и дисплей радио в машине (как у sahailee).
    /// </summary>
    public class StationMod : Script
    {
        private const string Off = "OFF";

        // Хеши — alloc8or/gta5-nativedb-data.
        private const ulong GET_PLAYER_RADIO_STATION_NAME = 0xF6D733C32076AD03;
        private const ulong IS_RADIO_RETUNING = 0xA151A7394A214E65;
        private const ulong IS_RADIO_FADED_OUT = 0x0626A247D2405330;
        private const ulong SET_VEH_RADIO_STATION = 0x1B9C0099CB942AC6;
        private const ulong SET_VEHICLE_RADIO_ENABLED = 0x3B988190C0AA6C0B;
        private const ulong FREEZE_RADIO_STATION = 0x344F393B027E38C3;
        private const ulong UNFREEZE_RADIO_STATION = 0xFC00454CF60B91DD;
        private const ulong SET_RADIO_AUTO_UNFREEZE = 0xC1AA9F53CE982990;
        private const ulong LOCK_RADIO_STATION = 0x477D9DB48F889591;
        private const ulong SET_USER_RADIO_CONTROL_ENABLED = 0x19F21E63AE6EAE4E;
        private const ulong START_AUDIO_SCENE = 0x013A80FC08F6E4F2;
        private const ulong STOP_AUDIO_SCENE = 0xDFE8422B3B94E688;
        private const ulong IS_AUDIO_SCENE_ACTIVE = 0xB65B60556E2A9225;
        private const ulong GET_MUSIC_VOL_SLIDER = 0x3A48AB4445D499BE;
        private const ulong IS_PAUSE_MENU_ACTIVE = 0xB0034A223497FFCB;
        private const ulong IS_CUTSCENE_PLAYING = 0xD3C2E180A40F031E;
        private const ulong IS_PLAYER_SWITCH_IN_PROGRESS = 0xD9D2CFFF49FAB35F;
        private const ulong IS_SCREEN_FADED_OUT = 0xB16FCE9DDC7BA182;
        private const ulong IS_SCRIPTED_CONVERSATION_ONGOING = 0x16754C556D2EDE3D;
        private const ulong IS_MOBILE_PHONE_CALL_ONGOING = 0x7497D2CE2C30D24C;
        private const ulong IS_ANY_SPEECH_PLAYING = 0x729072355FA39EC9;
        private const ulong IS_AMBIENT_SPEECH_PLAYING = 0x9072C8B49907BFAD;
        private const ulong GET_IS_VEHICLE_ENGINE_RUNNING = 0xAE31E7DF9B5B132E;
        private const ulong GET_CONVERTIBLE_ROOF_STATE = 0xF8C397922FC03F41;
        private const ulong GET_VEHICLE_CLASS = 0x29439776AAA00A62;
        private const ulong GET_ENTITY_SUBMERGED_LEVEL = 0xE81AFC1BC4CC41CE;
        private const ulong BEGIN_TEXT_COMMAND_THEFEED_POST = 0x202709F4C58A0424;
        private const ulong ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME = 0x6C188BE134E074AA;
        private const ulong END_TEXT_COMMAND_THEFEED_POST_TICKER = 0x2ED7843F8F801023;
        private const ulong REQUEST_SCALEFORM_MOVIE = 0x11FE353CF9733E6F;
        private const ulong HAS_SCALEFORM_MOVIE_LOADED = 0x85F01B8D5B90570E;
        private const ulong BEGIN_SCALEFORM_MOVIE_METHOD = 0xF6E48914C7A8694E;
        private const ulong SCALEFORM_MOVIE_METHOD_ADD_PARAM_PLAYER_NAME_STRING = 0xE83A3E3557A56640;
        private const ulong END_SCALEFORM_MOVIE_METHOD = 0xC6796A8FFA375E53;
        private const ulong GET_AUDIBLE_MUSIC_TRACK_TEXT_ID = 0x50B196FC9ED6545B;
        private const ulong GET_FILENAME_FOR_AUDIO_CONVERSATION = 0x7B5280EBA9840C72; // текст метки (const char*)

        // Свои метки в global.gxt2 (добавляет установщик): сюда пишется артист и трек плеера,
        // а метки трека Pop ("<id>A" / "<id>S") перенаправляются на них через SHVDN Enhanced
        // (Game.AddCustomGxtEntry) — так центр колеса показывает трек плеера.
        private const string ArtistLabel = "GTARADIO_ARTIST";
        private const string TitleLabel = "GTARADIO_TITLE";
        private const int LabelBytes = 127;

        // Настройки: scripts\GtaRadio.Mod.ini
        private readonly string _station;
        private readonly string _stationLabel;
        private readonly bool _useScene, _useFreeze, _useVehicleOff, _useDisableRadio;
        private readonly string[] _scenes;
        private readonly float _volumeDb, _sliderCurve, _duckDb, _interiorCutoff;
        private readonly bool _notify, _dashboard, _wheelTrack;
        private readonly Keys _keyPlayPause, _keyNext, _keyPrev;
        private readonly string _agentPath;

        private readonly int _fadeMs;
        private readonly int _output;

        private LinkPlayer? _player;
        private RadioLink? _link;
        private bool _started;
        private int _errors;
        private bool _owned, _holdingOff, _wasRetuning, _frozen;
        private string? _activeScene;
        private int _metaSeq = -1;
        private int _dashHandle;
        private DateTime _lastDash = DateTime.MinValue;
        private DateTime _lastAgentLaunch = DateTime.MinValue;
        private DateTime _agentMissingSince = DateTime.MinValue;
        private bool _toldNoAgent;
        private int _wheelTrackId;
        private uint _wheelA, _wheelS;
        private string _wheelArtist = "", _wheelTitle = "";
        private bool? _wheelLabelsPresent;
        private DateTime _lastWheelCheck = DateTime.MinValue;

        static StationMod()
        {
            Log("---- static ctor (assembly loaded by SHVDN)");
        }

        public StationMod()
        {
            // Первая строка — до любых нативов и звука: если дальше что-то упадёт, видно, докуда дошли.
            Log("ctor: start");
            _station = Settings.GetValue("Radio", "Station", "RADIO_02_POP");
            _stationLabel = Settings.GetValue("Radio", "StationLabel", "GTA Radio");
            var strategies = Settings.GetValue("Radio", "MuteStrategies", "Scene,Freeze").ToLowerInvariant();
            _useScene = strategies.Contains("scene");
            _useFreeze = strategies.Contains("freeze");
            _useVehicleOff = strategies.Contains("vehicleoff");
            _useDisableRadio = strategies.Contains("disableradio");
            _scenes = Settings.GetValue("Radio", "MuteScenes", "CAR_MOD_RADIO_MUTE_SCENE,MP_JOB_CHANGE_RADIO_MUTE,MUTES_RADIO_SCENE")
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _volumeDb = Settings.GetValue("Audio", "VolumeDb", -6.0f);
            _sliderCurve = Settings.GetValue("Audio", "SliderCurve", 2.0f);
            _duckDb = Settings.GetValue("Audio", "DialogueDuckDb", -11.0f);
            _interiorCutoff = Settings.GetValue("Audio", "InteriorCutoffHz", 15000f);
            _fadeMs = Settings.GetValue("Audio", "FadeMs", 90);
            // Game — поток играет игра (основной); Agent — поток играет агент (запасной);
            // Remote — без захвата: плеер играет сам, мод глушит Pop и ставит плеер на play/pause.
            string output = Settings.GetValue("Audio", "Output", "Game");
            _output = output.Equals("Agent", StringComparison.OrdinalIgnoreCase) ? RadioLink.OutputAgent
                    : output.Equals("Remote", StringComparison.OrdinalIgnoreCase) ? RadioLink.OutputRemote
                    : RadioLink.OutputGame;
            _notify = Settings.GetValue("Display", "TrackNotifications", true);
            _dashboard = Settings.GetValue("Display", "Dashboard", true);
            _wheelTrack = Settings.GetValue("Display", "WheelTrack", true);
            _keyPlayPause = Settings.GetValue("Controls", "PlayPause", Keys.F7);
            _keyNext = Settings.GetValue("Controls", "Next", Keys.F8);
            _keyPrev = Settings.GetValue("Controls", "Previous", Keys.F6);
            _agentPath = Path.Combine(BaseDirectory, "..", "GtaRadio", "GtaRadio.Agent.exe");

            // Нативы, общая память и звук — на первом тике после загрузки (Start), не в конструкторе.
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += (_, __) =>
            {
                try { Release(CurrentVehicle()); } catch { }
                try { ClearWheel(); } catch { }
                try { _link?.GameBeat(false); } catch { }
                try { _player?.Dispose(); } catch { }
            };
            Interval = 0;
            Log("ctor: ok, station=" + _station + " output=" + output);
        }

        private void Start()
        {
            _started = true;
            Log("start: game loaded");
            try
            {
                int rate = 48000;
                if (_output == RadioLink.OutputGame)
                {
                    try { rate = LinkPlayer.DeviceSampleRate(); }
                    catch (Exception ex) { Log("[ERROR] device: " + ex.Message); }
                }
                _link = RadioLink.Create(rate);
                Log("start: link ok, rate=" + rate);
            }
            catch (Exception ex) { Log("[ERROR] link: " + ex); }

            if (_link != null && _output == RadioLink.OutputGame)
            {
                try
                {
                    _player = LinkPlayer.Open(_link, false, _fadeMs);
                    Log("start: audio ok (game output)");
                }
                catch (Exception ex) { Log("[ERROR] audio: " + ex); }
            }

            try
            {
                Function.Call((Hash)LOCK_RADIO_STATION, _station, false);
                Function.Call((Hash)SET_USER_RADIO_CONTROL_ENABLED, true);
            }
            catch (Exception ex) { Log("[ERROR] radio natives: " + ex.Message); }
            Log("[OK] loaded, station=" + _station + " audio=" + (_player != null) + " output=" + _output);
        }

        private static Vehicle? CurrentVehicle()
        {
            var ped = Game.Player.Character;
            if (ped == null || !ped.Exists()) return null;
            var v = ped.CurrentVehicle;
            return v != null && v.Exists() ? v : null;
        }

        private static string StationName()
        {
            string? name = Function.Call<string>((Hash)GET_PLAYER_RADIO_STATION_NAME);
            return string.IsNullOrEmpty(name) ? Off : name!;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_started)
            {
                // SHV 3351+ запускает скрипты только после экрана загрузки; ждём персонажа.
                if (Game.Player.Character == null || !Game.Player.Character.Exists()) return;
                Start();
            }
            try { Frame(); }
            catch (Exception ex)
            {
                // Исключение из Tick остановило бы скрипт: пишем первые 20 и продолжаем.
                if (_errors++ < 20) Log("[ERROR] tick: " + ex);
            }
        }

        private void Frame()
        {
            var veh = CurrentVehicle();
            bool retuning = Function.Call<bool>((Hash)IS_RADIO_RETUNING);
            string station = StationName();

            // Ownership — station_ownership.cpp образца; плюс: после перестройки OFF больше не наш,
            // чтобы выбранный игроком OFF не считался нашей станцией.
            if (_wasRetuning && !retuning && station != _station) _holdingOff = false;
            _wasRetuning = retuning;
            // Пешком станция остаётся за нами (как у образца): вернулся в машину с OFF — снова GTA Radio.
            bool owned;
            if (veh == null || retuning) owned = _owned;
            else owned = station == _station || (_holdingOff && station == Off);

            if (owned && !_owned) Acquire();
            else if (!owned && _owned) Release(veh);
            _owned = owned;

            if (_owned && !retuning && veh != null) ApplyFrameMutes(veh);

            float gain = 0f, cutoff = 22050f;
            bool audible = false;
            if (_owned && veh != null) audible = Mix(veh, retuning, out gain, out cutoff);
            float level = gain * (float)Math.Pow(10, _volumeDb / 20.0);
            _player?.SetTarget(level, cutoff);
            _link?.SetMix(level, cutoff, _output);
            // Агенту — «эфир нужен»: вне машины, в паузе, на катсцене, без двигателя плеер через 1.2 с на паузе.
            _link?.GameBeat(audible || _owned && veh != null && retuning);

            if (_owned && veh != null) ShowTrack();
            if (_owned && _wheelTrack) UpdateWheel();
            else if (_wheelTrackId != 0) ClearWheel();
            EnsureAgent();
        }

        private void Acquire()
        {
            if (_useFreeze && !_frozen)
            {
                Function.Call((Hash)SET_RADIO_AUTO_UNFREEZE, false);
                Function.Call((Hash)FREEZE_RADIO_STATION, _station);
                _frozen = true;
            }
            if (_useScene && _activeScene == null)
            {
                foreach (var s in _scenes)
                {
                    Function.Call<bool>((Hash)START_AUDIO_SCENE, s);
                    if (Function.Call<bool>((Hash)IS_AUDIO_SCENE_ACTIVE, s)) { _activeScene = s; break; }
                    Function.Call((Hash)STOP_AUDIO_SCENE, s);
                }
                Log(_activeScene != null ? "mute scene: " + _activeScene : "[ERROR] no mute scene started");
            }
            _holdingOff = _useVehicleOff || _useDisableRadio;
            _metaSeq = -1;
            _lastDash = DateTime.MinValue;
        }

        private void ApplyFrameMutes(Vehicle veh)
        {
            if (_useDisableRadio) Function.Call((Hash)SET_VEHICLE_RADIO_ENABLED, veh, false);
            if (_useVehicleOff) Function.Call((Hash)SET_VEH_RADIO_STATION, veh, Off);
        }

        private void Release(Vehicle? veh)
        {
            _holdingOff = false;
            ClearWheel();
            if (_activeScene != null)
            {
                Function.Call((Hash)STOP_AUDIO_SCENE, _activeScene);
                _activeScene = null;
            }
            if (_frozen)
            {
                Function.Call((Hash)UNFREEZE_RADIO_STATION, _station);
                Function.Call((Hash)SET_RADIO_AUTO_UNFREEZE, true);
                _frozen = false;
            }
            if (_useDisableRadio && veh != null) Function.Call((Hash)SET_VEHICLE_RADIO_ENABLED, veh, true);
        }

        /// <summary>Громкость и срез фильтра «как у нативного радио» (GameAudioProbe образца). false — эфир не нужен.</summary>
        private bool Mix(Vehicle veh, bool retuning, out float gain, out float cutoff)
        {
            cutoff = _interiorCutoff;
            gain = 0f;
            bool held = Function.Call<bool>((Hash)IS_PAUSE_MENU_ACTIVE) ||
                        Function.Call<bool>((Hash)IS_CUTSCENE_PLAYING) ||
                        Function.Call<bool>((Hash)IS_PLAYER_SWITCH_IN_PROGRESS) ||
                        Function.Call<bool>((Hash)IS_SCREEN_FADED_OUT);
            if (held || !Function.Call<bool>((Hash)GET_IS_VEHICLE_ENGINE_RUNNING, veh)) return false;
            if (retuning || Function.Call<bool>((Hash)IS_RADIO_FADED_OUT) && !_holdingOff) return true;

            int raw = Function.Call<int>((Hash)GET_MUSIC_VOL_SLIDER);
            float slider = raw >= 0 && raw <= 10 ? raw / 10f : raw > 10 && raw <= 100 ? raw / 100f : 1f;
            gain = (float)Math.Pow(slider, _sliderCurve);

            var player = Game.Player.Character;
            if (Function.Call<bool>((Hash)IS_SCRIPTED_CONVERSATION_ONGOING) ||
                Function.Call<bool>((Hash)IS_MOBILE_PHONE_CALL_ONGOING) ||
                Function.Call<bool>((Hash)IS_ANY_SPEECH_PLAYING, player) ||
                Function.Call<bool>((Hash)IS_AMBIENT_SPEECH_PLAYING, player))
                gain *= (float)Math.Pow(10, _duckDb / 20.0);

            int roof = Function.Call<int>((Hash)GET_CONVERTIBLE_ROOF_STATE, veh);
            int cls = Function.Call<int>((Hash)GET_VEHICLE_CLASS, veh);
            if (roof == 1 || roof == 2 || roof == 3 || cls == 8 || cls == 13 || cls == 14) cutoff = 22050f;

            float submerged = Function.Call<float>((Hash)GET_ENTITY_SUBMERGED_LEVEL, player);
            if (submerged > 0.35f) { cutoff = 700f; gain *= 0.5f; }
            return true;
        }

        /// <summary>Трек в штатных местах: лента уведомлений при смене трека и дисплей радио в машине.</summary>
        private void ShowTrack()
        {
            if (_link == null || !_link.AgentAlive) return;
            int seq = _link.MetaSeq;
            string artist = _link.Artist, title = _link.Title;
            string line = artist.Length > 0 ? artist + " - " + title : title;

            if (seq != _metaSeq)
            {
                _metaSeq = seq;
                if (_notify && line.Length > 0) FeedPost(_stationLabel + ": " + line);
                _lastDash = DateTime.MinValue;
            }
            // Игра сама перерисовывает дисплей (на OFF), поэтому обновляем его раз в секунду.
            if (_dashboard && (DateTime.UtcNow - _lastDash).TotalMilliseconds > 1000)
            {
                _lastDash = DateTime.UtcNow;
                Dashboard(_stationLabel, artist, title);
            }
        }

        /// <summary>Центр колеса: метки текущего (замороженного) трека Pop -> наши метки с треком плеера.</summary>
        private void UpdateWheel()
        {
            if (_link == null || !_link.AgentAlive) return;
            if (_wheelLabelsPresent == null)
            {
                _wheelLabelsPresent = Game.GetLocalizedString(unchecked((int)Joaat(ArtistLabel))).Length > 0 &&
                                      Game.GetLocalizedString(unchecked((int)Joaat(TitleLabel))).Length > 0;
                Log("wheel labels present: " + _wheelLabelsPresent);
            }

            int id = Function.Call<int>((Hash)GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
            if (id != 0 && id != -1 && id != _wheelTrackId)
            {
                ClearWheel();
                string key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (Game.GetLocalizedString(key + "A").Length == 0)
                    key = unchecked((uint)id).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _wheelA = Joaat(key + "A");
                _wheelS = Joaat(key + "S");
                // Без своих меток (установщик не дописал gxt2) — хотя бы пустая строка вместо чужого трека.
                uint toA = _wheelLabelsPresent == true ? Joaat(ArtistLabel) : 0u;
                uint toS = _wheelLabelsPresent == true ? Joaat(TitleLabel) : 0u;
                Game.UpdateCustomGxtEntry(_wheelA, toA);
                Game.UpdateCustomGxtEntry(_wheelS, toS);
                _wheelTrackId = id;
                Log("wheel track " + key + " -> GTA Radio labels");
            }
            if (_wheelLabelsPresent != true) return;

            string artist = _link.Artist, title = _link.Title;
            var now = DateTime.UtcNow;
            if (artist == _wheelArtist && title == _wheelTitle && (now - _lastWheelCheck).TotalMilliseconds < 1000) return;
            _lastWheelCheck = now;
            if (WriteLabel(ArtistLabel, artist, _wheelArtist)) _wheelArtist = artist;
            if (WriteLabel(TitleLabel, title, _wheelTitle)) _wheelTitle = title;
        }

        /// <summary>
        /// Пишет текст в свою метку прямо в загруженной таблице текстов, с нулём на конце.
        /// Game.SetLocalizedString добивает строку пробелами до исходной длины — в центре колеса
        /// текст уехал бы вбок. Пишем, только если по указателю наша метка: пробелы-плейсхолдер
        /// (после загрузки/смены языка) или наш прошлый текст; не больше LabelBytes байт.
        /// </summary>
        private static bool WriteLabel(string label, string text, string? previous)
        {
            IntPtr p = Function.Call<IntPtr>((Hash)GET_FILENAME_FOR_AUDIO_CONVERSATION, label);
            if (p == IntPtr.Zero) return false;
            var cur = new byte[LabelBytes + 1];
            Marshal.Copy(p, cur, 0, cur.Length);
            int len = Array.IndexOf(cur, (byte)0);
            if (len < 0) return false;
            string now = System.Text.Encoding.UTF8.GetString(cur, 0, len);
            string fitted = FitUtf8(text);
            if (now == fitted) return true;
            bool placeholder = len == LabelBytes && now.Trim().Length == 0;
            if (!placeholder && now != FitUtf8(previous ?? "")) return false;

            var bytes = System.Text.Encoding.UTF8.GetBytes(fitted);
            var buf = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, buf, 0, bytes.Length);
            Marshal.Copy(buf, 0, p, buf.Length);
            return true;
        }

        private void ClearWheel()
        {
            if (_wheelTrackId == 0) return;
            try
            {
                Game.RemoveCustomGxtEntry(_wheelA, out _);
                Game.RemoveCustomGxtEntry(_wheelS, out _);
            }
            catch (Exception ex) { Log("[ERROR] wheel clear: " + ex.Message); }
            _wheelTrackId = 0;
        }

        /// <summary>Обрезка по границе символа: SetLocalizedString режет байты и иначе ломает кириллицу.</summary>
        private static string FitUtf8(string text)
        {
            text ??= "";
            var enc = System.Text.Encoding.UTF8;
            while (enc.GetByteCount(text) > LabelBytes - 1) text = text.Substring(0, text.Length - 1);
            return text;
        }

        private static uint Joaat(string s)
        {
            uint h = 0;
            foreach (byte c in System.Text.Encoding.UTF8.GetBytes(s.ToLowerInvariant()))
            {
                unchecked { h += c; h += h << 10; h ^= h >> 6; }
            }
            unchecked { h += h << 3; h ^= h >> 11; h += h << 15; }
            return h;
        }

        private static void FeedPost(string text)
        {
            Function.Call((Hash)BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call((Hash)ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call<int>((Hash)END_TEXT_COMMAND_THEFEED_POST_TICKER, false, false);
        }

        // Аргументы SET_RADIO — как у sahailee/GTA-Spotify-Radio: ("", станция, исполнитель, трек).
        private void Dashboard(string station, string artist, string title)
        {
            if (_dashHandle == 0) _dashHandle = Function.Call<int>((Hash)REQUEST_SCALEFORM_MOVIE, "dashboard");
            if (_dashHandle == 0 || !Function.Call<bool>((Hash)HAS_SCALEFORM_MOVIE_LOADED, _dashHandle)) return;
            if (!Function.Call<bool>((Hash)BEGIN_SCALEFORM_MOVIE_METHOD, _dashHandle, "SET_RADIO")) return;
            foreach (var p in new[] { "", station, artist, title })
                Function.Call((Hash)SCALEFORM_MOVIE_METHOD_ADD_PARAM_PLAYER_NAME_STRING, p);
            Function.Call((Hash)END_SCALEFORM_MOVIE_METHOD);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_owned || _link == null) return;
            if (e.KeyCode == _keyPlayPause) _link.SendCommand(RadioLink.CmdPlayPause);
            else if (e.KeyCode == _keyNext) _link.SendCommand(RadioLink.CmdNext);
            else if (e.KeyCode == _keyPrev) _link.SendCommand(RadioLink.CmdPrev);
        }

        /// <summary>Агент живёт вместе с игрой: мод запускает его сам, агент закрывается после игры.</summary>
        private void EnsureAgent()
        {
            if (_link == null) return;
            if (_link.AgentAlive) { _agentMissingSince = DateTime.MinValue; _toldNoAgent = false; return; }
            if (_link.AgentState == RadioLink.StateUserExit) return; // выключен пользователем из трея
            var now = DateTime.UtcNow;
            if (_agentMissingSince == DateTime.MinValue) { _agentMissingSince = now; return; }
            if ((now - _agentMissingSince).TotalSeconds < 3 || (now - _lastAgentLaunch).TotalSeconds < 30) return;
            _lastAgentLaunch = now;
            try
            {
                string path = Path.GetFullPath(_agentPath);
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) });
                    Log("agent started: " + path);
                }
                else if (!_toldNoAgent)
                {
                    _toldNoAgent = true;
                    Log("[ERROR] agent not found: " + path);
                    FeedPost(_stationLabel + ": агент GtaRadio не найден, переустановите мод");
                }
            }
            catch (Exception ex) { Log("[ERROR] agent start: " + ex.Message); }
        }

        private static void Log(string msg)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "mod.log"), DateTime.Now.ToString("HH:mm:ss.fff ") + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}
