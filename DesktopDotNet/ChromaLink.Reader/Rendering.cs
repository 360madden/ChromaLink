namespace ChromaLink.Reader;

public static class StripRenderer
{
    public static Bgr24Frame Render(StripProfile profile, ReadOnlySpan<byte> transportBytes, string sourceKind = "synthetic")
    {
        if (transportBytes.Length != profile.PayloadBytes)
        {
            throw new ArgumentException($"Expected {profile.PayloadBytes} transport bytes.", nameof(transportBytes));
        }

        var frame = Bgr24Frame.CreateSolid(profile.BandWidth, profile.BandHeight, Bgr24Color.White, sourceKind);
        var matrix = BuildMatrix(profile, transportBytes);
        for (var row = 0; row < profile.GridRows; row++)
        {
            for (var column = 0; column < profile.GridColumns; column++)
            {
                if (!matrix[row, column])
                {
                    continue;
                }

                var startX = profile.QuietLeft + (column * profile.Pitch);
                var startY = profile.QuietTop + (row * profile.Pitch);
                for (var y = startY; y < startY + profile.Pitch; y++)
                {
                    for (var x = startX; x < startX + profile.Pitch; x++)
                    {
                        frame.SetColor(x, y, Bgr24Color.Black);
                    }
                }
            }
        }

        return frame;
    }

    public static bool[,] BuildMatrix(StripProfile profile, ReadOnlySpan<byte> transportBytes)
    {
        var matrix = new bool[profile.GridRows, profile.GridColumns];
        for (var column = 0; column < profile.GridColumns; column++)
        {
            matrix[0, column] = true;
            matrix[profile.GridRows - 1, column] = column % 2 == 0;
        }

        for (var row = 0; row < profile.GridRows; row++)
        {
            matrix[row, 0] = true;
            matrix[row, profile.GridColumns - 1] = row % 2 == 0;
        }

        var duplicateBits = BitPacking.BytesToBits(TransportCodec.DuplicateHeaderBytes(transportBytes));
        var payloadBits = BitPacking.BytesToBits(transportBytes);
        var leftDuplicateCursor = 0;
        var rightDuplicateCursor = 0;
        var payloadCursor = 0;
        for (var row = 1; row < profile.GridRows - 1; row++)
        {
            for (var interiorColumn = 1; interiorColumn <= profile.InteriorColumns; interiorColumn++)
            {
                var absoluteColumn = interiorColumn;
                if (interiorColumn <= profile.MetadataColumnsPerSide)
                {
                    matrix[row, absoluteColumn] = duplicateBits[leftDuplicateCursor++] == 1;
                }
                else if (interiorColumn > profile.MetadataColumnsPerSide + profile.PayloadColumns)
                {
                    matrix[row, absoluteColumn] = duplicateBits[rightDuplicateCursor++] == 1;
                }
                else
                {
                    matrix[row, absoluteColumn] = payloadBits[payloadCursor++] == 1;
                }
            }
        }

        return matrix;
    }
}
