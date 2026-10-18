using System.Media;

namespace TuneIt;

internal sealed class ReferenceTone : IDisposable
{
    private SoundPlayer? player;
    private MemoryStream? stream;

    internal void Play(double frequency, int repeatSeconds, bool bassInstrument)
    {
        Stop();
        const int sampleRate = 44100;
        var samples = sampleRate * repeatSeconds;
        var bytes = new byte[44 + samples * 2];
        using (var writer = new BinaryWriter(new MemoryStream(bytes, true)))
        {
            writer.Write("RIFF"u8.ToArray()); writer.Write(bytes.Length - 8); writer.Write("WAVEfmt "u8.ToArray());
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
            writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
            var attackSeconds = .008;
            var decaySeconds = bassInstrument ? 4.8 : 3.2;
            var random = new Random((int)Math.Round(frequency * 100));
            for (var i = 0; i < samples; i++)
            {
                var time = i / (double)sampleRate;
                var attack = Math.Min(1.0, time / attackSeconds);
                var release = Math.Min(1.0, (samples - 1 - i) / (sampleRate * .025));
                var envelope = attack * release * Math.Exp(-time / decaySeconds);
                var phase = 2 * Math.PI * frequency * i / sampleRate;
                double value = bassInstrument
                    ? Math.Sin(phase) * .56 + Math.Sin(phase * 2) * .27 + Math.Sin(phase * 3) * .12 + Math.Sin(phase * 4) * .05
                    : Math.Sin(phase) * .58 + Math.Sin(phase * 2) * .22 + Math.Sin(phase * 3) * .11 + Math.Sin(phase * 4) * .06 + Math.Sin(phase * 5) * .03;
                // A very short, quiet pick transient gives the note a clear beginning.
                if (time < .018) value += (random.NextDouble() * 2 - 1) * .10 * (1 - time / .018);
                writer.Write((short)(value * envelope * 15500));
            }
        }
        stream = new MemoryStream(bytes, false);
        player = new SoundPlayer(stream);
        player.Load();
        player.PlayLooping();
    }

    internal void Stop() { player?.Stop(); player?.Dispose(); player = null; stream?.Dispose(); stream = null; }
    public void Dispose() => Stop();
}
