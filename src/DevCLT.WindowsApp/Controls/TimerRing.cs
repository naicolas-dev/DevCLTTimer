using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DevCLT.WindowsApp.Controls;

/// <summary>
/// A circular progress ring control. Track + progress arc, 2px stroke.
/// </summary>
public class TimerRing : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(TimerRing),
            new PropertyMetadata(0.0, OnProgressChanged));

    public static readonly DependencyProperty IsWarningProperty =
        DependencyProperty.Register(nameof(IsWarning), typeof(bool), typeof(TimerRing),
            new PropertyMetadata(false, OnWarningChanged));

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool IsWarning { get => (bool)GetValue(IsWarningProperty); set => SetValue(IsWarningProperty, value); }

    private readonly Ellipse _track;
    private readonly Path _arc;
    private readonly double _size = 280;
    private readonly double _strokeWidth = 2;

    public TimerRing()
    {
        Width = _size;
        Height = _size;

        var grid = new Grid();

        _track = new Ellipse
        {
            Width = _size,
            Height = _size,
            StrokeThickness = _strokeWidth,
            Fill = Brushes.Transparent
        };
        _track.SetResourceReference(Ellipse.StrokeProperty, "BorderBrush");

        _arc = new Path
        {
            StrokeThickness = _strokeWidth,
            Fill = Brushes.Transparent,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        _arc.SetResourceReference(Path.StrokeProperty, "PrimaryBrush");

        grid.Children.Add(_track);
        grid.Children.Add(_arc);
        Content = grid;

        UpdateArc();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TimerRing)d).UpdateArc();

    private static void OnWarningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TimerRing)d).UpdateWarningColor();

    private void UpdateWarningColor()
    {
        if (IsWarning)
            _arc.SetResourceReference(Path.StrokeProperty, "WarningBrush");
        else
            _arc.SetResourceReference(Path.StrokeProperty, "PrimaryBrush");
    }

    private void UpdateArc()
    {
        var progress = Math.Clamp(Progress, 0.0, 1.0);
        if (progress <= 0)
        {
            _arc.Data = Geometry.Empty;
            return;
        }

        if (progress >= 1.0)
            progress = 0.9999; // avoid full circle artifact

        var radius = (_size - _strokeWidth) / 2.0;
        var center = _size / 2.0;
        var angle = progress * 360.0;
        var radians = angle * Math.PI / 180.0;

        var startX = center;
        var startY = _strokeWidth / 2.0;

        var endX = center + radius * Math.Sin(radians);
        var endY = center - radius * Math.Cos(radians);

        var isLargeArc = angle > 180;

        var pathFigure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };
        pathFigure.Segments.Add(new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise,
        });

        _arc.Data = new PathGeometry(new[] { pathFigure });
    }
}
