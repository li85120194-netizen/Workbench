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
}
