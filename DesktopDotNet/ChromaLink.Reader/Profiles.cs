namespace ChromaLink.Reader;

public sealed record StripProfile(
    string Id,
    byte NumericId,
    int WindowWidth,
    int WindowHeight,
    int BandWidth,
    int BandHeight,
    int QuietLeft,
    int QuietRight,
    int QuietTop,
    int QuietBottom,
    int Pitch,
    int GridColumns,
    int GridRows,
    int MetadataColumnsPerSide,
    int PayloadColumns)
{
    public int InteriorColumns => GridColumns - 2;

    public int InteriorRows => GridRows - 2;

    public int MetadataCellsPerSide => MetadataColumnsPerSide * InteriorRows;

    public int PayloadCells => PayloadColumns * InteriorRows;

    public int PayloadBytes => PayloadCells / 8;
}

public static class StripProfiles
{
    public static readonly StripProfile P360A = new(
        "P360A",
        2,
        640,
        360,
        640,
        40,
        8,
        8,
        2,
        2,
        6,
        104,
        6,
        6,
        90);

    public static StripProfile Default => P360A;
}
