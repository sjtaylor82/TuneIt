namespace TuneIt;

internal static class PitchDetector
{
    // YIN-style difference analysis. Returns null when the signal is too quiet or unclear.
    internal static double? Detect(short[] samples, int count, int sampleRate, double? expectedHz = null)
    {
        if (count < 1024) return null;
        double meanSquare = 0;
        for (var i = 0; i < count; i++) meanSquare += (double)samples[i] * samples[i];
        if (Math.Sqrt(meanSquare / count) < 120) return null;

        var minHz = expectedHz.HasValue ? expectedHz.Value * 0.70 : 30;
        var maxHz = expectedHz.HasValue ? expectedHz.Value * 1.42 : 2500;
        var minLag = Math.Max(2, (int)(sampleRate / maxHz));
        var maxLag = Math.Min(count / 2 - 1, (int)Math.Ceiling(sampleRate / minHz));
        var diff = new double[maxLag + 1];
        for (var lag = 1; lag <= maxLag; lag++)
        {
            double sum = 0;
            for (var i = 0; i < count - maxLag; i++)
            {
                var d = samples[i] - samples[i + lag];
                sum += d * d;
            }
            diff[lag] = sum;
        }

        double running = 0;
        var bestLag = -1;
        var bestValue = double.MaxValue;
        for (var lag = 1; lag <= maxLag; lag++)
        {
            running += diff[lag];
            diff[lag] = running == 0 ? 1 : diff[lag] * lag / running;
            if (lag >= minLag && diff[lag] < bestValue) { bestValue = diff[lag]; bestLag = lag; }
        }
        if (!expectedHz.HasValue)
        {
            // For chromatic note identification choose the first strong local dip.
            // Choosing the global minimum can land on a multiple of the true period,
            // incorrectly reporting a note one or more octaves too low.
            for (var lag = Math.Max(minLag + 1, 2); lag < maxLag; lag++)
            {
                if (diff[lag] < 0.30 && diff[lag] <= diff[lag - 1] && diff[lag] < diff[lag + 1])
                {
                    bestLag = lag;
                    bestValue = diff[lag];
                    break;
                }
            }
        }
        if (bestLag < 0 || bestValue > 0.50) return null;

        double refined = bestLag;
        if (bestLag > 1 && bestLag < maxLag)
        {
            var a = diff[bestLag - 1]; var b = diff[bestLag]; var c = diff[bestLag + 1];
            var divisor = 2 * (2 * b - c - a);
            if (Math.Abs(divisor) > 1e-12) refined += (c - a) / divisor;
        }
        return sampleRate / refined;
    }
}
