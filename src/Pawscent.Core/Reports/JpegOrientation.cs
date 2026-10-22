using System.Buffers.Binary;

namespace Pawscent.Core.Reports;

/// <summary>
/// Reads the EXIF orientation of a JPEG. Phones store a photo as the sensor saw it and record the turn in this tag,
/// so without it receipts photographed upright would be drawn sideways.
/// </summary>
internal static class JpegOrientation
{
    /// <summary>EXIF orientation 1: stored upright.</summary>
    public const int Upright = 1;

    private const int OrientationTag = 0x0112;

    /// <summary>The orientation, 1 to 8, as defined by EXIF. <see cref="Upright"/> if the file has none.</summary>
    public static int Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static int Read(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            if (ReadUInt16(reader) != 0xFFD8)
                return Upright;
            // Walk the segments before the image data; EXIF is in an APP1 segment.
            while (true)
            {
                var marker = ReadUInt16(reader);
                if ((marker & 0xFF00) != 0xFF00 || marker is 0xFFDA or 0xFFD9)
                    return Upright;
                var length = ReadUInt16(reader);
                if (length < 2)
                    return Upright;
                var data = reader.ReadBytes(length - 2);
                if (data.Length < length - 2)
                    return Upright;
                if (marker == 0xFFE1 && FromExif(data) is { } orientation)
                    return orientation;
            }
        }
        catch (EndOfStreamException)
        {
            return Upright;
        }
    }

    private static int ReadUInt16(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2);
        return bytes.Length == 2 ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : throw new EndOfStreamException();
    }

    // An APP1 segment: "Exif\0\0", then a TIFF header (byte order, 42, offset of the first IFD) and the IFD entries.
    private static int? FromExif(byte[] data)
    {
        if (data.Length < 14 || !data.AsSpan(0, 6).SequenceEqual("Exif\0\0"u8))
            return null;
        var tiff = data.AsSpan(6);
        var littleEndian = tiff[0] == 'I' && tiff[1] == 'I';
        if (!littleEndian && !(tiff[0] == 'M' && tiff[1] == 'M'))
            return null;
        if (UInt16(tiff, 2, littleEndian) != 42)
            return null;
        var ifd = UInt32(tiff, 4, littleEndian);
        if (ifd > (uint)tiff.Length - 2)
            return null;
        var count = UInt16(tiff, (int)ifd, littleEndian);
        for (var i = 0; i < count; i++)
        {
            var entry = (int)ifd + 2 + i * 12;
            if (entry + 12 > tiff.Length)
                return null;
            if (UInt16(tiff, entry, littleEndian) == OrientationTag)
            {
                var value = UInt16(tiff, entry + 8, littleEndian);
                return value is >= 1 and <= 8 ? value : null;
            }
        }
        return null;
    }

    private static int UInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint UInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
}
