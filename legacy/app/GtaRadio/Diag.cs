using System.Diagnostics;

namespace GtaRadio;

internal static class Diag
{
    public static void W(string msg)
    {
        try
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "diag.log"), DateTime.Now.ToString("HH:mm:ss ") + msg + Environment.NewLine);
        }
        catch { }
    }

    public static void Verify(string token)
    {
        try
        {
            try
            {
                var list = Sources.ListAsync().GetAwaiter().GetResult();
                var s = list.FirstOrDefault(x => x.AppId.Contains("yandex", StringComparison.OrdinalIgnoreCase));
                if (s != null && !s.Playing) s.Session.TryPlayAsync().GetAwaiter().GetResult();
            }
            catch { }
            Thread.Sleep(1200);
            var procs = Process.GetProcesses().Where(p =>
            {
                try { return p.ProcessName.Contains(token, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            }).ToList();
            if (procs.Count == 0) { W("verify: no procs"); return; }
            var pids = procs.Select(p => (uint)p.Id).ToHashSet();
            var main = procs.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? procs[0];

            using var eng = new AudioEngine { Volume = 0.002f };
            var ok = eng.Start((uint)main.Id, pids, token, 0);
            W($"verify start ok={ok} out={eng.OutputName}");

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio", "loop.wav");
            using var cap = new NAudio.Wave.WasapiLoopbackCapture();
            using var writer = new NAudio.Wave.WaveFileWriter(path, cap.WaveFormat);
            cap.DataAvailable += (s2, e2) => { try { writer.Write(e2.Buffer, 0, e2.BytesRecorded); } catch { } };
            cap.StartRecording();
            Thread.Sleep(4000);
            cap.StopRecording();
            eng.Stop();
            W($"verify wrote {new FileInfo(path).Length} bytes");
        }
        catch (Exception ex) { W("verify ERR " + ex); }
    }

    public static void Fix(string token)
    {
        try
        {
            using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            int n = 0;
            foreach (var d in en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                try
                {
                    var ss = d.AudioSessionManager.Sessions;
                    for (int i = 0; i < ss.Count; i++)
                    {
                        try
                        {
                            var s = ss[i];
                            if (s.GetProcessID == 0) continue;
                            var p = Process.GetProcessById((int)s.GetProcessID);
                            if (!p.ProcessName.Contains(token, StringComparison.OrdinalIgnoreCase)) continue;
                            s.SimpleAudioVolume.Mute = false;
                            s.SimpleAudioVolume.Volume = 1f;
                            n++;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            W($"fixed {n} sessions for {token}");
        }
        catch (Exception ex) { W("ERR " + ex); }
    }

    public static void Record(string token, int seconds = 5)
    {
        try
        {
            var procs = Process.GetProcesses().Where(p =>
            {
                try { return p.ProcessName.Contains(token, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            }).ToList();
            if (procs.Count == 0) { W("rec: no procs"); return; }
            var pids = procs.Select(p => (uint)p.Id).ToHashSet();
            var main = procs.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? procs[0];
            try
            {
                var list = Sources.ListAsync().GetAwaiter().GetResult();
                var s = list.FirstOrDefault(x => x.AppId.Contains("yandex", StringComparison.OrdinalIgnoreCase) || x.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (s != null && !s.Playing) s.Session.TryPlayAsync().GetAwaiter().GetResult();
            }
            catch { }
            Thread.Sleep(1500);
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio", "rec.raw");
            var done = new ManualResetEventSlim(false);
            var th2 = new Thread(() =>
            {
                try
                {
                    var cap = ProcessLoopback.Open((uint)main.Id);
                    if (cap == null) { W("rec: capture failed"); return; }
                    using var fs = File.Create(path);
                    var cts = new CancellationTokenSource();
                    ProcessLoopback.Pump(cap, seconds, (buf, len) => { try { fs.Write(buf, 0, len); } catch { } }, cts.Token, null);
                    ProcessLoopback.Close(cap);
                }
                catch (Exception ex) { W("rec thread ERR " + ex); }
                finally { done.Set(); }
            });
            th2.SetApartmentState(ApartmentState.MTA);
            th2.Start();
            done.Wait(seconds * 1000 + 4000);
            W($"rec: wrote {(File.Exists(path) ? new FileInfo(path).Length : 0)} bytes");
        }
        catch (Exception ex) { W("rec ERR " + ex); }
    }

    public static void Mix(string token)
    {
        try
        {
            using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                try
                {
                    var f = d.AudioClient.MixFormat;
                    var ext = f as NAudio.Wave.WaveFormatExtensible;
                    W($"dev={d.FriendlyName} mix sr={f.SampleRate} bits={f.BitsPerSample} ch={f.Channels} enc={f.Encoding} sub={(ext != null ? ext.SubFormat.ToString() : "-")}");
                }
                catch (Exception ex) { W("dev err " + d.FriendlyName + " " + ex.Message); }
            }
        }
        catch (Exception ex) { W("ERR " + ex); }
    }

    public static void Unmute(string token)
    {
        try
        {
            using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            int n = 0;
            foreach (var d in en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                try
                {
                    var ss = d.AudioSessionManager.Sessions;
                    for (int i = 0; i < ss.Count; i++)
                    {
                        try
                        {
                            var s = ss[i];
                            if (s.GetProcessID == 0) continue;
                            var p = Process.GetProcessById((int)s.GetProcessID);
                            if (!p.ProcessName.Contains(token, StringComparison.OrdinalIgnoreCase)) continue;
                            s.SimpleAudioVolume.Mute = false;
                            n++;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            W($"unmuted {n} sessions for {token}");
        }
        catch (Exception ex) { W("ERR " + ex); }
    }

    public static void Run(string token, string? device = null)
    {
        try
        {
            try
            {
                var list = Sources.ListAsync().GetAwaiter().GetResult();
                var s = list.FirstOrDefault(x => x.AppId.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (s != null && !s.Playing) s.Session.TryPlayAsync().GetAwaiter().GetResult();
            }
            catch { }
            Thread.Sleep(1500);
            var procs = Process.GetProcesses().Where(p =>
            {
                try
                {
                    if (p.ProcessName.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
                    var f = p.MainModule?.FileName ?? "";
                    return f.Contains(token, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }).ToList();

            W($"token={token} procs={string.Join(",", procs.Select(p => p.ProcessName + ":" + p.Id))}");
            if (procs.Count == 0) { W("no processes"); return; }

            var pids = procs.Select(p => (uint)p.Id).ToHashSet();
            var mainProc = procs.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero) ?? procs[0];
            using var eng = new AudioEngine { Volume = 1f, DeviceMatch = device };
            var ok = eng.Start((uint)mainProc.Id, pids, token, 0);
            W($"start ok={ok} out={eng.OutputName} run={eng.IsRunning} main={mainProc.Id}");
            for (int i = 0; i < 8; i++)
            {
                Thread.Sleep(1000);
                W($"level={eng.Level:F1} run={eng.IsRunning}");
            }
            eng.Stop();
            W("stopped");
        }
        catch (Exception ex) { W("ERR " + ex); }
    }
}
