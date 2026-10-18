using TuneIt;

var notes = new[] { 41.2034, 55.0, 73.4162, 82.4069, 97.9989, 110.0, 146.8324, 195.9977, 246.9417, 329.6276 };
var failures = 0;
foreach (var target in notes)
foreach (var ratio in new[] { 0.94, 1.0, 1.06 })
{
    const int rate = 11025;
    var actual = target * ratio;
    var data = new short[2048];
    for (var i = 0; i < data.Length; i++)
    {
        var phase = 2 * Math.PI * actual * i / rate;
        data[i] = (short)(12000 * (Math.Sin(phase) + .35 * Math.Sin(phase * 2)) / 1.35);
    }
    var detected = PitchDetector.Detect(data, data.Length, rate, target);
    var error = detected.HasValue ? Math.Abs(1200 * Math.Log2(detected.Value / actual)) : 9999;
    Console.WriteLine($"Target {target,7:0.00}, actual {actual,7:0.00}, detected {detected,7:0.00}, error {error,5:0.0} cents");
    if (error > 8) failures++;
}
if (failures > 0) throw new Exception($"{failures} pitch tests failed");

foreach (var actual in new[] { 82.4069, 329.6276, 659.2551, 1318.5102, 1760.0 })
{
    const int rate = 22050;
    var data = new short[2048];
    for (var i = 0; i < data.Length; i++)
    {
        var phase = 2 * Math.PI * actual * i / rate;
        data[i] = (short)(12000 * (Math.Sin(phase) + .35 * Math.Sin(phase * 2)) / 1.35);
    }
    var detected = PitchDetector.Detect(data, data.Length, rate);
    var error = detected.HasValue ? Math.Abs(1200 * Math.Log2(detected.Value / actual)) : 9999;
    Console.WriteLine($"Chromatic actual {actual,7:0.00}, detected {detected,7:0.00}, error {error,5:0.0} cents");
    if (error > 15) throw new Exception($"Chromatic test failed at {actual:0.00} Hz");
}

foreach (var tone in Tunings.Guitar.Concat(Tunings.Bass))
{
    const int sourceRate = 44100;
    const int analysisRate = sourceRate / 2;
    var generated = new short[4096];
    for (var i = 0; i < generated.Length; i++)
    {
        var phase = 2 * Math.PI * tone.Frequency * i / sourceRate;
        var value = tone.Frequency < 110
            ? (Math.Sin(phase) * .60 + Math.Sin(phase * 2) * .25 + Math.Sin(phase * 3) * .15) * 19000
            : Math.Sin(phase) * 15000;
        generated[i] = (short)value;
    }
    var reduced = new short[2048];
    for (var i = 0; i < reduced.Length; i++) reduced[i] = generated[i * 2];
    var detected = PitchDetector.Detect(reduced, reduced.Length, analysisRate, tone.Frequency);
    var error = detected.HasValue ? Math.Abs(1200 * Math.Log2(detected.Value / tone.Frequency)) : 9999;
    Console.WriteLine($"Reference {tone.Name,-12} expected {tone.Frequency,7:0.00}, measured {detected,7:0.00}, error {error,5:0.0} cents");
    if (error > 2) throw new Exception($"Reference tone test failed for {tone.Name}");
}
