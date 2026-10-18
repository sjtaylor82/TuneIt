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

    internal static readonly NoteInfo[] GuitarEFlat =
    [
        new("Low E flat, E flat 2", 77.7817), new("A flat, A flat 2", 103.8262),
        new("D flat, D flat 3", 138.5913), new("G flat, G flat 3", 184.9972),
        new("B flat, B flat 3", 233.0819), new("High E flat, E flat 4", 311.1270)
    ];

    internal static readonly NoteInfo[] GuitarD =
    [
        new("Low D, D2", 73.4162), new("G, G2", 97.9989), new("C, C3", 130.8128),
        new("F, F3", 174.6141), new("A, A3", 220.0000), new("High D, D4", 293.6648)
    ];

    internal static readonly NoteInfo[] Bass =
    [
        new("Low E, E1", 41.2034), new("A, A1", 55.0000),
        new("D, D2", 73.4162), new("G, G2", 97.9989)
    ];
}
