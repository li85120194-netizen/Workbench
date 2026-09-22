using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Workbench;

public sealed record ImageConversionOptions(string Format, int JpegQuality, int MaxLongEdge, string OutputDirectory);

public static class ImageProcessingService
{
    public static Task<(int Success, int Failed, List<string> Errors)> ProcessAsync(IReadOnlyList<string> files, ImageConversionOptions options, IProgress<int>? progress = null)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(options.OutputDirectory);
            var success = 0;
            var errors = new List<string>();
            for (var index = 0; index < files.Count; index++)
            {
                try
                {
                    ConvertOne(files[index], options);
                    success++;
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(files[index]) + "：" + ex.Message);
                }
                progress?.Report((index + 1) * 100 / Math.Max(1, files.Count));
            }
            return (success, files.Count - success, errors);
        });
    }

    private static void ConvertOne(string path, ImageConversionOptions options)
    {
        BitmapFrame frame;
        using (var input = File.OpenRead(path))
        {
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame = decoder.Frames[0];
        }

        BitmapSource source = frame;
        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (options.MaxLongEdge > 0 && longest > options.MaxLongEdge)
        {
            var scale = options.MaxLongEdge / (double)longest;
            var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            transformed.Freeze();
            source = transformed;
        }

        var encoder = CreateEncoder(options);
        encoder.Frames.Add(BitmapFrame.Create(source));
        var extension = options.Format.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ".jpg",
            "png" => ".png",
            "bmp" => ".bmp",
            "tiff" => ".tiff",
            "gif" => ".gif",
            _ => ".jpg"
        };
        var output = GetUniquePath(options.OutputDirectory, Path.GetFileNameWithoutExtension(path) + extension);
        using var stream = File.Create(output);
        encoder.Save(stream);
    }

    private static BitmapEncoder CreateEncoder(ImageConversionOptions options) => options.Format.ToLowerInvariant() switch
    {
        "jpeg" or "jpg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(options.JpegQuality, 1, 100) },
        "png" => new PngBitmapEncoder(),
        "bmp" => new BmpBitmapEncoder(),
        "tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
        "gif" => new GifBitmapEncoder(),
        _ => new JpegBitmapEncoder { QualityLevel = Math.Clamp(options.JpegQuality, 1, 100) }
    };

    private static string GetUniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 1; ; index++)
        {
            path = Path.Combine(directory, $"{name}_{index}{extension}");
            if (!File.Exists(path)) return path;
        }
    }
}
