using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Workbench;

public static class PdfToolsService
{
    public static void Merge(IReadOnlyList<string> inputPaths, string outputPath)
    {
        using var output = new PdfDocument();
        foreach (var path in inputPaths)
        {
            using var input = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            for (var index = 0; index < input.PageCount; index++) output.AddPage(input.Pages[index]);
        }
        output.Save(outputPath);
    }

    public static int Split(string inputPath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        for (var index = 0; index < input.PageCount; index++)
        {
            using var output = new PdfDocument();
            output.AddPage(input.Pages[index]);
            output.Save(GetUniquePath(outputDirectory, $"{baseName}_第{index + 1:000}页.pdf"));
        }
        return input.PageCount;
    }

    public static int Extract(string inputPath, string outputPath, string rangeText)
    {
        using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        var indexes = ParsePageRange(rangeText, input.PageCount);
        using var output = new PdfDocument();
        foreach (var index in indexes) output.AddPage(input.Pages[index]);
        output.Save(outputPath);
        return indexes.Count;
    }

    public static int Rotate(string inputPath, string outputPath, int degrees)
    {
        using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        for (var index = 0; index < input.PageCount; index++)
        {
            var page = output.AddPage(input.Pages[index]);
            page.Rotate = ((page.Rotate + degrees) % 360 + 360) % 360;
        }
        output.Save(outputPath);
        return input.PageCount;
    }

    private static IReadOnlyList<int> ParsePageRange(string text, int pageCount)
    {
        var result = new SortedSet<int>();
        foreach (var rawPart in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Replace('，', ',');
            if (part.Contains('-'))
            {
                var bounds = part.Split('-', 2, StringSplitOptions.TrimEntries);
                if (bounds.Length != 2 || !int.TryParse(bounds[0], out var start) || !int.TryParse(bounds[1], out var end))
                    throw new FormatException("页码范围格式不正确。");
                if (start > end) (start, end) = (end, start);
                for (var page = start; page <= end; page++) AddPage(page);
            }
            else if (int.TryParse(part, out var page)) AddPage(page);
            else throw new FormatException("页码范围格式不正确。");
        }
        if (result.Count == 0) throw new FormatException("请至少输入一个页码。");
        return result.ToList();

        void AddPage(int page)
        {
            if (page < 1 || page > pageCount) throw new ArgumentOutOfRangeException(nameof(text), $"页码 {page} 超出范围，文档共 {pageCount} 页。");
            result.Add(page - 1);
        }
    }

    private static string GetUniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(fileName);
        for (var index = 1; ; index++)
        {
            path = Path.Combine(directory, $"{name}_{index}.pdf");
            if (!File.Exists(path)) return path;
        }
    }
}
