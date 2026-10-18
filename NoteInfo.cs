namespace TuneIt;

internal sealed record NoteInfo(string Name, double Frequency)
{
    public override string ToString() => $"{Name} — {Frequency:0.00} hertz";
}

internal static class Tunings
{
    internal static readonly NoteInfo[] Guitar =
    [
        new("Low E, E2", 82.4069), new("A, A2", 110.0000), new("D, D3", 146.8324),
        new("G, G3", 195.9977), new("B, B3", 246.9417), new("High E, E4", 329.6276)
    ];

    internal static readonly NoteInfo[] Bass =
    [
        new("Low E, E1", 41.2034), new("A, A1", 55.0000),
        new("D, D2", 73.4162), new("G, G2", 97.9989)
    ];
}
