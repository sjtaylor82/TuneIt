using System.Collections.Concurrent;
using System.Text.Json;
using Vosk;

namespace TuneIt;

internal sealed class VoiceCommandListener : IDisposable
{
    private readonly BlockingCollection<byte[]> audio = new(12);
    private readonly CancellationTokenSource cancellation = new();
    private readonly object sync = new();
    private Task? worker;
    private volatile bool active;
    internal event Action? NextRecognized;
    internal event Action? SpeechStarted;

    internal void Start()
    {
        active = true;
        lock (sync) worker ??= Task.Run(ProcessAudio);
    }

    internal void Stop()
    {
        active = false;
        while (audio.TryTake(out _)) { }
    }

    internal void Feed(short[] samples, int count)
    {
        if (!active || audio.IsAddingCompleted) return;
        var bytes = new byte[count * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        if (!audio.TryAdd(bytes)) audio.TryTake(out _);
    }

    private void ProcessAudio()
    {
        Vosk.Vosk.SetLogLevel(-1);
        var modelPath = Path.Combine(AppContext.BaseDirectory, "speech-model", "vosk-model-small-en-us-0.15");
        using var model = new Model(modelPath);
        using var recognizer = new VoskRecognizer(model, 44100,
            "[\"next\", \"no\", \"now\", \"never\", \"name\", \"nine\", \"nice\", \"new\", \"[unk]\"]");
        recognizer.SetWords(true);
        try
        {
            foreach (var bytes in audio.GetConsumingEnumerable(cancellation.Token))
            {
                if (!active) continue;
                var final = recognizer.AcceptWaveform(bytes, bytes.Length);
                var result = final ? recognizer.Result() : recognizer.PartialResult();
                var key = final ? "text" : "partial";
                string? word = null;
                double confidence = 0;
                try
                {
                    using var json = JsonDocument.Parse(result);
                    if (json.RootElement.TryGetProperty(key, out var value)) word = value.GetString();
                    if (final && json.RootElement.TryGetProperty("result", out var words) && words.GetArrayLength() == 1)
                    {
                        var item = words[0];
                        if (item.TryGetProperty("conf", out var conf)) confidence = conf.GetDouble();
                    }
                }
                catch (JsonException) { }
                if (!string.Equals(word?.Trim(), "next", StringComparison.OrdinalIgnoreCase)) continue;
                SpeechStarted?.Invoke();
                if (final && confidence >= 0.75) NextRecognized?.Invoke();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        active = false;
        cancellation.Cancel();
        audio.CompleteAdding();
        try { worker?.Wait(2000); } catch { }
        cancellation.Dispose();
        audio.Dispose();
    }
}
