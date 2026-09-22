using System.IO;
using System.Text.RegularExpressions;

namespace Workbench;

public sealed class RenamePreviewItem
{
    public string FullPath { get; init; } = string.Empty;
    public string OldName { get; init; } = string.Empty;
    public string NewName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool CanRename => Status == "可重命名";
}

public sealed record RenameOptions(string Find, string Replace, string Prefix, string Suffix, bool UseRegex, bool AddNumber, int NumberStart);

public static class BatchRenameService
{
    public static IReadOnlyList<RenamePreviewItem> Preview(IEnumerable<string> paths, RenameOptions options)
    {
        var files = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToList();
        Regex? regex = null;
        if (options.UseRegex && !string.IsNullOrEmpty(options.Find)) regex = new Regex(options.Find, RegexOptions.CultureInvariant);
        var proposals = new List<(string Source, string NewName, string Target, bool IsValid)>();
        for (var index = 0; index < files.Count; index++)
        {
            var path = files[index];
            var directory = Path.GetDirectoryName(path)!;
            var extension = Path.GetExtension(path);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(options.Find))
                name = regex is null ? name.Replace(options.Find, options.Replace, StringComparison.CurrentCulture) : regex.Replace(name, options.Replace);
            name = options.Prefix + name + options.Suffix;
            if (options.AddNumber) name += "_" + (options.NumberStart + index).ToString("000");
            var newName = name + extension;
            var target = Path.Combine(directory, newName);
            var isValid = !string.IsNullOrWhiteSpace(name) && newName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            proposals.Add((path, newName, target, isValid));
        }

        var duplicateTargets = proposals.GroupBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var movingSources = proposals
            .Where(item => !string.Equals(item.Source, item.Target, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return proposals.Select(item =>
        {
            var status = !item.IsValid ? "名称无效"
                : duplicateTargets.Contains(item.Target) ? "目标名称重复"
                : string.Equals(item.Source, item.Target, StringComparison.OrdinalIgnoreCase) ? "无变化"
                : File.Exists(item.Target) && !movingSources.Contains(item.Target) ? "目标文件已存在"
                : "可重命名";
            return new RenamePreviewItem { FullPath = item.Source, OldName = Path.GetFileName(item.Source), NewName = item.NewName, Status = status };
        }).ToList();
    }

    public static (int Renamed, string Message) Apply(IReadOnlyList<RenamePreviewItem> preview)
    {
        if (preview.Any(item => item.Status is not ("可重命名" or "无变化")))
            return (0, "预览中存在冲突，请调整规则后再执行。");

        var changing = preview.Where(item => item.CanRename).ToList();
        var moved = new List<(string Original, string Temporary, string Target)>();
        try
        {
            foreach (var item in changing)
            {
                var temporary = Path.Combine(Path.GetDirectoryName(item.FullPath)!, ".toolbox-rename-" + Guid.NewGuid().ToString("N") + Path.GetExtension(item.FullPath));
                var target = Path.Combine(Path.GetDirectoryName(item.FullPath)!, item.NewName);
                File.Move(item.FullPath, temporary);
                moved.Add((item.FullPath, temporary, target));
            }

            foreach (var item in moved) File.Move(item.Temporary, item.Target);
            return (moved.Count, $"已完成 {moved.Count} 个文件的重命名。");
        }
        catch (Exception ex)
        {
            foreach (var item in moved.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(item.Temporary) && !File.Exists(item.Original)) File.Move(item.Temporary, item.Original);
                    else if (File.Exists(item.Target) && !File.Exists(item.Original)) File.Move(item.Target, item.Original);
                }
                catch { }
            }
            return (0, "重命名失败，已尽可能恢复原文件：" + ex.Message);
        }
    }
}
