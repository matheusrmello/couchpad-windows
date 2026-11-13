using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace CouchPad.Platform.Windows;

/// <summary>Explicitly invoked diagnostic tone on the current Windows default output.</summary>
public static class WindowsAudioTestTone
{
    public static void PlayOnEndpoint(string endpointId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        var enumerator = (WindowsAudioProbe.IMMDeviceEnumerator)new WindowsAudioProbe.MMDeviceEnumerator();
        try
        {
            WindowsAudioProbe.Check(enumerator.GetDevice(endpointId, out var device));
            try
            {
                WindowsAudioProbe.Check(device.GetState(out var state));
                if (state != 1) throw new InvalidOperationException("The selected endpoint is not available.");
                var iid = typeof(IAudioClient).GUID;
                WindowsAudioProbe.Check(device.Activate(ref iid, 23, IntPtr.Zero, out var client));
                try { Render((IAudioClient)client); }
                finally { Marshal.ReleaseComObject(client); }
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    [SupportedOSPlatform("windows")]
    private static void Render(IAudioClient client)
    {
        const int rate = 48000;
        const int sampleCount = rate;
        const uint convertPcm = 0x80000000;
        const uint highQualityResampling = 0x08000000;
        const uint noPersist = 0x00080000;
        var format = new WaveFormat { Tag = 1, Channels = 1, Rate = rate, BytesPerSecond = rate * 2, BlockAlign = 2, Bits = 16 };
        WindowsAudioProbe.Check(client.Initialize(0, convertPcm | highQualityResampling | noPersist,
            2_000_000, 0, ref format, IntPtr.Zero));
        WindowsAudioProbe.Check(client.GetBufferSize(out var capacity));
        if (capacity == 0) throw new InvalidOperationException("The audio buffer is empty.");
        var iid = typeof(IAudioRenderClient).GUID;
        WindowsAudioProbe.Check(client.GetService(ref iid, out var service));
        try
        {
            var render = (IAudioRenderClient)service;
            var samples = new short[sampleCount];
            for (var i = 0; i < samples.Length; i++)
            {
                var fade = Math.Min(1.0, Math.Min(i, samples.Length - 1 - i) / 1200.0);
                samples[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 3500 * fade);
            }

            var written = WriteSamples(render, samples, 0, capacity);
            WindowsAudioProbe.Check(client.Start());
            try
            {
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(10);
                    WindowsAudioProbe.Check(client.GetCurrentPadding(out var queued));
                    if (written == samples.Length && queued == 0) return;
                    if (written < samples.Length && queued < capacity)
                        written += WriteSamples(render, samples, written, capacity - queued);
                }
                throw new TimeoutException("The selected audio endpoint did not finish playing.");
            }
            finally { _ = client.Stop(); }
        }
        finally { Marshal.ReleaseComObject(service); }
    }

    private static int WriteSamples(IAudioRenderClient render, short[] samples, int offset, uint space)
    {
        var count = (int)Math.Min(space, (uint)(samples.Length - offset));
        WindowsAudioProbe.Check(render.GetBuffer((uint)count, out var buffer));
        try { Marshal.Copy(samples, offset, buffer, count); }
        finally { WindowsAudioProbe.Check(render.ReleaseBuffer((uint)count, 0)); }
        return count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort Tag, Channels;
        public uint Rate, BytesPerSecond;
        public ushort BlockAlign, Bits, ExtraSize;
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int mode, uint flags, long bufferDuration, long period, ref WaveFormat format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }

    public static void Play()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        const int rate = 22050;
        const int samples = rate * 2;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
            writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(samples * 2);
            for (var i = 0; i < samples; i++)
            {
                var fade = Math.Min(1.0, Math.Min(i, samples - 1 - i) / 500.0);
                writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 5000 * fade));
            }
        }
        if (!PlaySound(stream.ToArray(), IntPtr.Zero, 0x0004 | 0x0002))
            throw new InvalidOperationException("Audio diagnostic tone could not be played.");
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] sound, IntPtr module, uint flags);
}
