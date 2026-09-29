using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Workbench;
public static class LocalOcrService
{
    public static async Task<string> RecognizeAsync(string path)
    {
        var file=await StorageFile.GetFileFromPathAsync(path);
        using var stream=await file.OpenAsync(FileAccessMode.Read);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
        var engine=OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("当前系统未安装可用的 OCR 语言包。");
        var result=await engine.RecognizeAsync(bitmap);
        return result.Text;
    }
}