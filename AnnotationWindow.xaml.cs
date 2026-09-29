using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Workbench;

public partial class AnnotationWindow : Window
{
    private readonly System.Windows.Media.Brush _brush;
    private Polyline? _activeLine;

    public AnnotationWindow(System.Windows.Media.Color color)
    {
        InitializeComponent();
        _brush = new SolidColorBrush(color);
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _activeLine = new Polyline
        {
            Stroke = _brush,
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        _activeLine.Points.Add(e.GetPosition(DrawingCanvas));
        DrawingCanvas.Children.Add(_activeLine);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_activeLine is not null && e.LeftButton == MouseButtonState.Pressed)
            _activeLine.Points.Add(e.GetPosition(DrawingCanvas));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _activeLine = null;
        ReleaseMouseCapture();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastStroke();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _activeLine = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        DrawingCanvas.Children.Clear();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.F2)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            Clear_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            UndoLastStroke();
        }
    }

    private void UndoLastStroke()
    {
        _activeLine = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (DrawingCanvas.Children.Count > 0)
            DrawingCanvas.Children.RemoveAt(DrawingCanvas.Children.Count - 1);
    }}
