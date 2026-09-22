using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench;

public sealed class ToolboxState
{
    public bool ClipboardMonitoringEnabled { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public List<ClipboardEntry> ClipboardHistory { get; set; } = new();
    public List<NoteItem> Notes { get; set; } = new();
}

public sealed class ClipboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Text { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var value = Text.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > 90 ? value[..90] + "…" : value;
        }
    }

    [JsonIgnore]
    public string TimeText => CreatedAt.ToString("MM-dd HH:mm:ss");
}

public sealed class NoteItem : INotifyPropertyChanged
{
    private string _title = "新便签";
    private string _content = string.Empty;
    private bool _isDone;

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 300;

    public string Title
    {
        get => _title;
        set { if (_title == value) return; _title = value; OnPropertyChanged(); }
    }

    public string Content
    {
        get => _content;
        set { if (_content == value) return; _content = value; OnPropertyChanged(); }
    }

    public bool IsDone
    {
        get => _isDone;
        set { if (_isDone == value) return; _isDone = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    [JsonIgnore]
    public string StatusText => IsDone ? "已完成" : "进行中";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class StateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly string DirectoryPath = GetStateDirectory();
    private static readonly string StatePath = Path.Combine(DirectoryPath, "state.json");

    private static string GetStateDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("TOOLBOX_STATE_DIR");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Toolbox")
            : overridePath;
    }

    public static ToolboxState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new ToolboxState();
            return JsonSerializer.Deserialize<ToolboxState>(File.ReadAllText(StatePath), Options) ?? new ToolboxState();
        }
        catch
        {
            return new ToolboxState();
        }
    }

    public static void Save(ToolboxState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporaryPath = StatePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, Options));
        File.Move(temporaryPath, StatePath, true);
    }
}
