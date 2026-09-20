using System.Text.Json;
using System.IO;

namespace Workbench;

public sealed record OrganizeResult(int Moved, int Failed);
public sealed record UndoResult(string Message);

public static class DesktopOrganizerService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".doc", ".docx", ".docm", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ppt", ".pptx", ".pptm", ".pdf", ".txt", ".csv", ".rtf", ".wps", ".wpt", ".et", ".ett", ".dps", ".dpt" };
    private static string StateDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Workbench");
    private static string StateFile => Path.Combine(StateDir, "organizer-last-run.json");
    public static string DesktopPath => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string TodayFolder => Path.Combine(DesktopPath, DateTime.Today.ToString("yyyy-MM-dd"));

    public static List<FileInfo> GetCandidates()
    {
        var desktop = new DirectoryInfo(DesktopPath);
        if (!desktop.Exists) return new();
        return desktop.EnumerateFiles().Where(f => Extensions.Contains(f.Extension) && (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0).OrderBy(f => f.Name).ToList();
    }

    public static OrganizeResult Organize()
    {
        Directory.CreateDirectory(TodayFolder); Directory.CreateDirectory(StateDir);
        var records = new List<MoveRecord>(); int failed = 0;
        foreach (var file in GetCandidates())
        {
            try
            {
                string destination = UniquePath(Path.Combine(TodayFolder, file.Name));
                File.Move(file.FullName, destination); records.Add(new(file.FullName, destination));
            }
            catch { failed++; }
        }
        File.WriteAllText(StateFile, JsonSerializer.Serialize(records));
        return new(records.Count, failed);
    }

    public static UndoResult UndoLast()
    {
        if (!File.Exists(StateFile)) return new("没有可撤回的执行记录。");
        try
        {
            var records = JsonSerializer.Deserialize<List<MoveRecord>>(File.ReadAllText(StateFile)) ?? new(); int restored = 0, failed = 0;
            foreach (var record in records.AsEnumerable().Reverse())
            {
                try { if (!File.Exists(record.Destination)) { failed++; continue; } File.Move(record.Destination, UniquePath(record.Source)); restored++; } catch { failed++; }
            }
            File.Delete(StateFile);
            return new($"已恢复 {restored} 项，失败 {failed} 项。");
        }
        catch (Exception ex) { return new($"撤回失败：{ex.Message}"); }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        string? folder = Path.GetDirectoryName(path); string name = Path.GetFileNameWithoutExtension(path); string ext = Path.GetExtension(path);
        for (int i = 2; i < 10000; i++) { string candidate = Path.Combine(folder!, $"{name} ({i}){ext}"); if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate; }
        throw new IOException("无法生成不重名路径。");
    }
    private sealed record MoveRecord(string Source, string Destination);
}
