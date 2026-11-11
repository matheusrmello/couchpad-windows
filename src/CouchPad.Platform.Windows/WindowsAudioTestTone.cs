using System.Runtime.InteropServices;

namespace CouchPad.Platform.Windows;

/// <summary>Explicitly invoked diagnostic tone on the current Windows default output.</summary>
public static class WindowsAudioTestTone
{
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
