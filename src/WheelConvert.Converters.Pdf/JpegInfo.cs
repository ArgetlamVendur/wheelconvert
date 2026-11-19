namespace WheelConvert.Converters.Pdf;

/// <summary>Frame header of a JPEG (the SOFn marker): enough to check what a PDF image really contains.</summary>
internal readonly record struct JpegInfo(int Width, int Height, int Precision, int Components)
{
    /// <summary>Reads the frame header, or returns null when <paramref name="data"/> isn't a JPEG this code understands.</summary>
    public static JpegInfo? TryRead(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return null;
        }

        var i = 2;
        while (i + 4 <= data.Length)
        {
            if (data[i] != 0xFF)
            {
                return null;
            }

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++; // fill byte
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2; // standalone markers carry no length
                continue;
            }

            var length = (data[i + 2] << 8) | data[i + 3];
            var isFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isFrame)
            {
                if (length < 8 || i + 10 > data.Length)
                {
                    return null;
                }

                var height = (data[i + 5] << 8) | data[i + 6];
                var width = (data[i + 7] << 8) | data[i + 8];
                return new JpegInfo(width, height, data[i + 4], data[i + 9]);
            }

            if (marker == 0xDA)
            {
                return null; // scan data reached without a frame header
            }

            i += 2 + length;
        }

        return null;
    }
}
