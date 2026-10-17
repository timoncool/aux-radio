using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GtaRadio;

/// <summary>
/// Захват звука конкретного процесса и его дочерних (Application Loopback, Win10 2004+).
/// </summary>
internal static class ProcessLoopback
{
    private const string VAD = @"VAD\Process_Loopback";
    private const int AUDCLNT_SHAREMODE_SHARED = 0;
    private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    private const int PROCESS_LOOPBACK = 1;
    private const int INCLUDE_TREE = 1;
    private const ushort VT_BLOB = 65;

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_ACTIVATION_PARAMS
    {
        public int ActivationType;
        public uint TargetProcessId;
        public int ProcessTreeScope;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public uint blobSize;
        [FieldOffset(16)] public IntPtr blobData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr format, IntPtr audioSessionGuid);
        int GetBufferSize(out uint frames);
        int GetStreamLatency(out long latency);
        int GetCurrentPadding(out uint frames);
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        int GetMixFormat(out IntPtr format);
        int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr handle);
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        int ReleaseBuffer(uint frames);
        int GetNextPacketSize(out uint frames);
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [In] ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    private sealed class Handler : IActivateAudioInterfaceCompletionHandler
    {
        public readonly ManualResetEventSlim Done = new(false);
        public IAudioClient? Client;
        public int Hr = -1;
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation op)
        {
            try
            {
                op.GetActivateResult(out Hr, out object iface);
                Client = iface as IAudioClient;
            }
            catch { }
            Done.Set();
        }
    }

    public sealed class Handle
    {
        public required IAudioClient Client;
        public required IAudioCaptureClient Capture;
        public required WAVEFORMATEX Format;
    }

    public static Handle? Open(uint pid)
    {
        var guid = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        var parms = new AUDIOCLIENT_ACTIVATION_PARAMS
        {
            ActivationType = PROCESS_LOOPBACK,
            TargetProcessId = pid,
            ProcessTreeScope = INCLUDE_TREE
        };
        int size = Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>();
        IntPtr blob = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(parms, blob, false);
        var pv = new PROPVARIANT { vt = VT_BLOB, blobSize = (uint)size, blobData = blob };
        IntPtr pvPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PROPVARIANT>());
        Marshal.StructureToPtr(pv, pvPtr, false);

        var handler = new Handler();
        int hr = ActivateAudioInterfaceAsync(VAD, ref guid, pvPtr, handler, out _);
        if (hr != 0) return null;
        if (!handler.Done.Wait(5000)) return null;
        if (handler.Hr != 0 || handler.Client == null) return null;
        var client = handler.Client;

        WAVEFORMATEX fmt = new()
        {
            wFormatTag = 3,
            nChannels = 2,
            nSamplesPerSec = 48000,
            wBitsPerSample = 32,
            nBlockAlign = 8,
            nAvgBytesPerSec = 48000 * 8,
            cbSize = 0
        };
        IntPtr fmtPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEFORMATEX>());
        Marshal.StructureToPtr(fmt, fmtPtr, false);
        hr = client.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 2_000_000, 0, fmtPtr, IntPtr.Zero);
        if (hr != 0)
        {
            fmt.wFormatTag = 1;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 4;
            fmt.nAvgBytesPerSec = 48000 * 4;
            Marshal.StructureToPtr(fmt, fmtPtr, false);
            hr = client.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 2_000_000, 0, fmtPtr, IntPtr.Zero);
            if (hr != 0) return null;
        }

        var capGuid = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        hr = client.GetService(ref capGuid, out object capObj);
        if (hr != 0 || capObj is not IAudioCaptureClient cap) return null;

        hr = client.Start();
        if (hr != 0) return null;

        return new Handle { Client = client, Capture = cap, Format = fmt };
    }

    /// <summary>Читает пакеты и отдаёт их в onData; блокирующий вызов на durationSeconds.</summary>
    public static void Pump(Handle h, int seconds, Action<byte[], int> onData, CancellationToken ct, Action<double>? onLevel = null)
    {
        double sumSq = 0; long n = 0;
        int bytesPerSample = h.Format.wBitsPerSample / 8;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds && !ct.IsCancellationRequested)
        {
            h.Capture.GetNextPacketSize(out uint packetFrames);
            while (packetFrames > 0)
            {
                if (h.Capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _) != 0) break;
                int bytes = (int)frames * h.Format.nBlockAlign;
                if ((flags & 2) == 0 && bytes > 0)
                {
                    var buf = new byte[bytes];
                    Marshal.Copy(data, buf, 0, bytes);
                    onData(buf, bytes);
                    for (int i = 0; i + bytesPerSample <= bytes; i += bytesPerSample)
                    {
                        short v = bytesPerSample == 4
                            ? (short)(Marshal.PtrToStructure<float>(data + i) * 32767)
                            : Marshal.ReadInt16(data + i);
                        sumSq += (double)v * v; n++;
                    }
                }
                h.Capture.ReleaseBuffer(frames);
                h.Capture.GetNextPacketSize(out packetFrames);
            }
            if (onLevel != null && n > 0 && sw.ElapsedMilliseconds % 500 < 20)
                onLevel(Math.Sqrt(sumSq / Math.Max(1, n)));
            Thread.Sleep(4);
        }
    }

    public static void Close(Handle h)
    {
        try { h.Client.Stop(); } catch { }
    }
}
