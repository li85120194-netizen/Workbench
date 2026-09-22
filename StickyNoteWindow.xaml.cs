using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Workbench;

public partial class StickyNoteWindow : Window
{
    private readonly NoteItem _note;
    private readonly Action _save;
    private bool _loading = true;

    public StickyNoteWindow(NoteItem note, Action save)
    {
        InitializeComponent();
        _note = note;
        _save = save;
        TitleBox.Text = note.Title;
        ContentBox.Text = note.Content;
        DoneCheck.IsChecked = note.IsDone;
        Width = Math.Max(MinWidth, note.Width);
        Height = Math.Max(MinHeight, note.Height);
        if (!double.IsNaN(note.Left)) Left = note.Left;
        if (!double.IsNaN(note.Top)) Top = note.Top;
        _loading = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Editor_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _note.Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "未命名便签" : TitleBox.Text.Trim();
        _note.Content = ContentBox.Text;
        _note.IsDone = DoneCheck.IsChecked == true;
        SavePosition();
        _save();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePosition();
        _save();
        base.OnClosing(e);
    }

    private void SavePosition()
    {
        _note.Left = Left;
        _note.Top = Top;
        _note.Width = ActualWidth;
        _note.Height = ActualHeight;
    }
}
