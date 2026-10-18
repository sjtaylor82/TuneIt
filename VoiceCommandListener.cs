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
    internal event Action? Ready;
    internal event Action<string>? Failed;

    internal void Start()
    {
        active = true;
        var modelPath = GetModelPath();
        if (!Directory.Exists(modelPath))
            throw new DirectoryNotFoundException("The voice-command files were not found. Extract the whole TuneIt ZIP before opening TuneIt.");
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
        try
        {
            Vosk.Vosk.SetLogLevel(-1);
            using var model = new Model(GetModelPath());
            using var recognizer = new VoskRecognizer(model, 44100,
                "[\"next\", \"no\", \"now\", \"never\", \"name\", \"nine\", \"nice\", \"new\", \"[unk]\"]");
            recognizer.SetWords(true);
            Ready?.Invoke();
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
        catch (Exception ex)
        {
            active = false;
            Failed?.Invoke(ex.Message);
        }
    }

    private static string GetModelPath() =>
        Path.Combine(AppContext.BaseDirectory, "speech-model", "vosk-model-small-en-us-0.15");

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
