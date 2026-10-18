using System.Runtime.InteropServices;

namespace TuneIt;

internal sealed class AudioInput : IDisposable
{
    private const int SampleRate = 44100;
    private const int BufferSamples = 4096;
    private const int WIM_DATA = 0x3C0;
    private readonly object sync = new();
    private readonly WaveInProc callback;
    private readonly List<BufferState> buffers = [];
    private IntPtr handle;
    private bool running;
    internal event Action<short[], int, int>? SamplesAvailable;

    internal AudioInput() => callback = OnWaveIn;

    internal void Start()
    {
        if (running) return;
        var format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSec = SampleRate, AvgBytesPerSec = SampleRate * 2, BlockAlign = 2, BitsPerSample = 16 };
        Check(waveInOpen(out handle, -1, ref format, callback, IntPtr.Zero, 0x00030000), "Could not open the default microphone");
        try
        {
            for (var n = 0; n < 3; n++)
            {
                var state = new BufferState(BufferSamples);
                state.Header.Data = state.DataHandle.AddrOfPinnedObject();
                state.Header.BufferLength = BufferSamples * 2;
                state.HeaderHandle = GCHandle.Alloc(state.Header, GCHandleType.Pinned);
                Check(waveInPrepareHeader(handle, state.HeaderHandle.AddrOfPinnedObject(), Marshal.SizeOf<WaveHeader>()), "Could not prepare microphone buffer");
                Check(waveInAddBuffer(handle, state.HeaderHandle.AddrOfPinnedObject(), Marshal.SizeOf<WaveHeader>()), "Could not queue microphone buffer");
                buffers.Add(state);
            }
            running = true;
            Check(waveInStart(handle), "Could not start microphone capture");
        }
        catch { Stop(); throw; }
    }

    internal void Stop()
    {
        IntPtr handleToClose;
        lock (sync)
        {
            running = false;
            handleToClose = handle;
            handle = IntPtr.Zero;
        }

        // Reset can wait for the audio callback. Never hold sync here: the callback
        // also takes it before deciding whether to queue another buffer.
        if (handleToClose != IntPtr.Zero)
        {
            waveInReset(handleToClose);
            waveInClose(handleToClose);
        }

        lock (sync)
        {
            foreach (var b in buffers) b.Dispose();
            buffers.Clear();
        }
    }

    private void OnWaveIn(IntPtr waveIn, int message, IntPtr instance, IntPtr headerPointer, IntPtr reserved)
    {
        if (message != WIM_DATA || headerPointer == IntPtr.Zero) return;
        var header = Marshal.PtrToStructure<WaveHeader>(headerPointer)!;
        var count = Math.Min(header.BytesRecorded / 2, BufferSamples);
        var copy = new short[count];
        Marshal.Copy(header.Data, copy, 0, count);
        SamplesAvailable?.Invoke(copy, count, SampleRate);
        lock (sync)
            if (running && handle != IntPtr.Zero) waveInAddBuffer(handle, headerPointer, Marshal.SizeOf<WaveHeader>());
    }

    private static void Check(int code, string message) { if (code != 0) throw new InvalidOperationException($"{message} (audio error {code})."); }
    public void Dispose() => Stop();

    private sealed class BufferState : IDisposable
    {
        internal readonly short[] Data;
        internal readonly WaveHeader Header = new();
        internal readonly GCHandle DataHandle;
        internal GCHandle HeaderHandle;
        internal BufferState(int size) { Data = new short[size]; DataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned); }
        public void Dispose() { if (HeaderHandle.IsAllocated) HeaderHandle.Free(); if (DataHandle.IsAllocated) DataHandle.Free(); }
    }

    private delegate void WaveInProc(IntPtr waveIn, int message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);
    [StructLayout(LayoutKind.Sequential)] private struct WaveFormat { public ushort FormatTag, Channels; public uint SamplesPerSec, AvgBytesPerSec; public ushort BlockAlign, BitsPerSample; public ushort Size; }
    [StructLayout(LayoutKind.Sequential)] private sealed class WaveHeader { public IntPtr Data; public int BufferLength, BytesRecorded; public IntPtr User; public int Flags, Loops; public IntPtr Next, Reserved; }
    [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr handle, int deviceId, ref WaveFormat format, WaveInProc callback, IntPtr instance, int flags);
    [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr handle, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr handle, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr handle);
    [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr handle);
}
