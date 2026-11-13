// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Calculates the alpha-weighted average color of every pixel in a user-selected
/// widget background image. Callers persist the result with the image path, so the
/// decoder is not used during ordinary rendering or later loads of that same image.
/// </summary>
public static class BackgroundImageColorAnalyzer
{
    public static bool TryCalculateAverageColor(string? imagePath, out Color averageColor)
    {
        averageColor = Colors.Transparent;

        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                imagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return false;
            }

            var source = decoder.Frames[0];
            if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return false;
            }

            var pixels = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var stride = checked(pixels.PixelWidth * 4);
            var rowBuffer = new byte[stride];

            long weightedRed = 0;
            long weightedGreen = 0;
            long weightedBlue = 0;
            long alphaWeight = 0;

            for (var row = 0; row < pixels.PixelHeight; row++)
            {
                pixels.CopyPixels(
                    new Int32Rect(0, row, pixels.PixelWidth, 1),
                    rowBuffer,
                    stride,
                    0);

                for (var index = 0; index < rowBuffer.Length; index += 4)
                {
                    var alpha = rowBuffer[index + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    weightedBlue += rowBuffer[index] * alpha;
                    weightedGreen += rowBuffer[index + 1] * alpha;
                    weightedRed += rowBuffer[index + 2] * alpha;
                    alphaWeight += alpha;
                }
            }

            if (alphaWeight == 0)
            {
                return false;
            }

            averageColor = Color.FromRgb(
                (byte)(weightedRed / alphaWeight),
                (byte)(weightedGreen / alphaWeight),
                (byte)(weightedBlue / alphaWeight));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
