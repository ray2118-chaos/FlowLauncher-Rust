using System;
using System.Buffers;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Infrastructure.Image
{
    public interface IImageHashGenerator
    {
        string GetHashFromImage(ImageSource image);
    }

    public class ImageHashGenerator : IImageHashGenerator
    {
        public string GetHashFromImage(ImageSource imageSource)
        {
            if (imageSource is not BitmapSource { IsFrozen: true } image)
            {
                return null;
            }

            try
            {
                // Normalize to a deterministic pixel format (32-bit premultiplied BGRA)
                BitmapSource normalized = image;
                if (normalized.Format != PixelFormats.Pbgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = normalized;
                    converted.DestinationFormat = PixelFormats.Pbgra32;
                    converted.EndInit();
                    converted.Freeze();

                    normalized = converted;
                }

                // Since we forced Pbgra32, we know it is exactly 4 bytes per pixel.
                const int bytesPerPixel = 4;

                var stride = normalized.PixelWidth * bytesPerPixel;
                var bufferSize = stride * normalized.PixelHeight;

                if (bufferSize <= 0)
                    return null;

                var rentedPixelBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    normalized.CopyPixels(rentedPixelBuffer, stride, 0);
                    var hash = Native.FlowNativeMethods.HashXxh364(rentedPixelBuffer.AsSpan(0, bufferSize));
                    return hash.ToString("X16");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedPixelBuffer);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
