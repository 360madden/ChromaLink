namespace ChromaLink.Reader;

public readonly record struct Bgr24Color(byte B, byte G, byte R)
{
    public static readonly Bgr24Color White = new(245, 245, 245);

    public static readonly Bgr24Color Black = new(16, 16, 16);
}

public sealed record class Bgr24Frame
{
    public Bgr24Frame(int width, int height, byte[] pixels, string sourceKind = "memory")
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (pixels.Length != width * height * 3)
        {
            throw new ArgumentException("Pixel buffer length did not match width*height*3.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
        SourceKind = sourceKind;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public string SourceKind { get; init; }

    public string CaptureRouteReason { get; init; } = string.Empty;

    public static Bgr24Frame CreateSolid(int width, int height, Bgr24Color color, string sourceKind = "synthetic")
    {
        var pixels = new byte[width * height * 3];
        for (var index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
        }

        return new Bgr24Frame(width, height, pixels, sourceKind);
    }

    public Bgr24Color GetColor(int x, int y)
    {
        var clampedX = Math.Clamp(x, 0, Width - 1);
        var clampedY = Math.Clamp(y, 0, Height - 1);
        var offset = ((clampedY * Width) + clampedX) * 3;
        return new Bgr24Color(Pixels[offset], Pixels[offset + 1], Pixels[offset + 2]);
    }

    public void SetColor(int x, int y, Bgr24Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        var offset = ((y * Width) + x) * 3;
        Pixels[offset] = color.B;
        Pixels[offset + 1] = color.G;
        Pixels[offset + 2] = color.R;
    }

    public byte GetGray(int x, int y)
    {
        var color = GetColor(x, y);
        return (byte)((color.R * 299 + color.G * 587 + color.B * 114) / 1000);
    }

    public double GetGrayKernel(double x, double y, int radius)
    {
        if (radius <= 0)
        {
            return GetGray((int)Math.Round(x), (int)Math.Round(y));
        }

        double total = 0;
        var count = 0;
        for (var yOffset = -radius; yOffset <= radius; yOffset++)
        {
            for (var xOffset = -radius; xOffset <= radius; xOffset++)
            {
                total += GetGray((int)Math.Round(x) + xOffset, (int)Math.Round(y) + yOffset);
                count++;
            }
        }

        return count == 0 ? 0 : total / count;
    }

    public Bgr24Frame Crop(int x, int y, int width, int height, string sourceKind = "crop")
    {
        var cropWidth = Math.Clamp(width, 1, Width - x);
        var cropHeight = Math.Clamp(height, 1, Height - y);
        var pixels = new byte[cropWidth * cropHeight * 3];
        for (var row = 0; row < cropHeight; row++)
        {
            var sourceOffset = (((y + row) * Width) + x) * 3;
            var destOffset = row * cropWidth * 3;
            Buffer.BlockCopy(Pixels, sourceOffset, pixels, destOffset, cropWidth * 3);
        }

        return new Bgr24Frame(cropWidth, cropHeight, pixels, sourceKind);
    }

    public Bgr24Frame Copy(string sourceKind = "clone")
    {
        return new Bgr24Frame(Width, Height, (byte[])Pixels.Clone(), sourceKind);
    }

    public byte[] ToPaddedBottomUpRows()
    {
        var paddedStride = ((Width * 3) + 3) & ~3;
        var result = new byte[paddedStride * Height];
        var rowStride = Width * 3;
        for (var row = 0; row < Height; row++)
        {
            var sourceOffset = (Height - row - 1) * rowStride;
            var destOffset = row * paddedStride;
            Buffer.BlockCopy(Pixels, sourceOffset, result, destOffset, rowStride);
        }

        return result;
    }

    public static Bgr24Frame FromPaddedBottomUpRows(int width, int height, byte[] rows, string sourceKind)
    {
        var paddedStride = ((width * 3) + 3) & ~3;
        var pixels = new byte[width * height * 3];
        var rowStride = width * 3;
        for (var row = 0; row < height; row++)
        {
            var sourceOffset = row * paddedStride;
            var destOffset = (height - row - 1) * rowStride;
            Buffer.BlockCopy(rows, sourceOffset, pixels, destOffset, rowStride);
        }

        return new Bgr24Frame(width, height, pixels, sourceKind);
    }
}

public static class BmpIO
{
    public static Bgr24Frame Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16() != 0x4D42)
        {
            throw new InvalidDataException("Not a BMP file.");
        }

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        var pixelOffset = reader.ReadUInt32();
        var dibHeaderSize = reader.ReadUInt32();
        if (dibHeaderSize < 40)
        {
            throw new InvalidDataException("Unsupported BMP header.");
        }

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var planes = reader.ReadUInt16();
        var bitsPerPixel = reader.ReadUInt16();
        var compression = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        if (planes != 1 || bitsPerPixel != 24 || compression != 0)
        {
            throw new InvalidDataException("Only uncompressed 24-bit BMP files are supported.");
        }

        stream.Position = pixelOffset;
        var paddedStride = ((width * 3) + 3) & ~3;
        var rows = reader.ReadBytes(paddedStride * Math.Abs(height));
        return Bgr24Frame.FromPaddedBottomUpRows(width, Math.Abs(height), rows, "bmp");
    }

    public static void Save(string path, Bgr24Frame frame)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var paddedRows = frame.ToPaddedBottomUpRows();
        var pixelOffset = 54;
        var totalSize = pixelOffset + paddedRows.Length;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4D42);
        writer.Write(totalSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(pixelOffset);
        writer.Write(40);
        writer.Write(frame.Width);
        writer.Write(frame.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(paddedRows.Length);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(paddedRows);
    }
}
