using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CorelTram;

public partial class MainWindow
{
    // ========== STROKE STYLE ==========
    private void InitializeStrokePresets()
    {
        AddStrokePreset(null, 1.5, PenLineCap.Flat);
        AddStrokePreset(null, 4.0, PenLineCap.Flat);
        AddStrokePreset(new DoubleCollection { 1, 2 }, 2.5, PenLineCap.Round);
        AddStrokePreset(new DoubleCollection { 4, 3 }, 2.0, PenLineCap.Flat);
        AddStrokePreset(new DoubleCollection { 6, 2, 1, 2 }, 2.0, PenLineCap.Flat);
        AddStrokePreset(new DoubleCollection { 8, 4 }, 3.0, PenLineCap.Flat);
    }

    private void AddStrokePreset(DoubleCollection? dash, double width, PenLineCap cap)
    {
        var line = new Line
        {
            X1 = 6,
            Y1 = 15,
            X2 = 54,
            Y2 = 15,
            Stroke = (Brush)FindResource("TextPrimary"),
            StrokeThickness = Math.Min(width, 4),
            StrokeStartLineCap = cap,
            StrokeEndLineCap = cap,
            StrokeDashArray = dash,
            SnapsToDevicePixels = true
        };
        var canvas = new Canvas { Width = 60, Height = 30, Background = Brushes.Transparent };
        canvas.Children.Add(line);

        var btn = new Button
        {
            Content = canvas,
            Width = 64,
            Height = 36,
            Margin = new Thickness(2),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            Style = (Style)FindResource("GhostButtonStyle")
        };
        btn.Click += (_, _) => ApplyStrokePreset(dash, width, cap);
        StrokePresetsPanel.Children.Add(btn);
    }

    private void ApplyStrokePreset(DoubleCollection? dash, double width, PenLineCap cap)
    {
        if (CurrentShapeForStyle() is Shape shape)
        {
            shape.StrokeThickness = width;
            shape.StrokeDashArray = dash;
            shape.StrokeStartLineCap = cap;
            shape.StrokeEndLineCap = cap;
        }
    }

    private Shape? CurrentShapeForStyle()
    {
        if (_selectedElement is Shape selected) return selected;
        if (_currentShape is Shape current) return current;
        return null;
    }

    private DoubleCollection? GetDashArray()
    {
        var tag = (LineStyleCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Solid";
        return tag switch
        {
            "Dash" => new DoubleCollection { 4, 3 },
            "Dot" => new DoubleCollection { 1, 2 },
            "DashDot" => new DoubleCollection { 6, 2, 1, 2 },
            "DashDotDot" => new DoubleCollection { 6, 2, 1, 2, 1, 2 },
            "LongDash" => new DoubleCollection { 8, 4 },
            _ => null
        };
    }

    private PenLineCap GetCap()
    {
        var tag = (LineCapCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Round";
        return tag switch
        {
            "Flat" => PenLineCap.Flat,
            "Square" => PenLineCap.Square,
            _ => PenLineCap.Round
        };
    }

    private PenLineJoin GetJoin()
    {
        var tag = (LineJoinCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Round";
        return tag switch
        {
            "Miter" => PenLineJoin.Miter,
            "Bevel" => PenLineJoin.Bevel,
            _ => PenLineJoin.Round
        };
    }

    private void ApplyStrokeStyleTo(Shape shape)
    {
        shape.StrokeDashArray = GetDashArray();
        var cap = GetCap();
        shape.StrokeStartLineCap = cap;
        shape.StrokeEndLineCap = cap;
        shape.StrokeLineJoin = GetJoin();
        shape.StrokeMiterLimit = 4;
    }

    private void LineStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CurrentShapeForStyle() is Shape shape)
            shape.StrokeDashArray = GetDashArray();
    }

    private void LineCap_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CurrentShapeForStyle() is Shape shape)
        {
            var cap = GetCap();
            shape.StrokeStartLineCap = cap;
            shape.StrokeEndLineCap = cap;
        }
    }

    private void LineJoin_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CurrentShapeForStyle() is Shape shape)
            shape.StrokeLineJoin = GetJoin();
    }

    private void StrokeThickness_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (StrokeThicknessCombo?.SelectedItem is not ComboBoxItem item) return;
        if (!double.TryParse(item.Content.ToString()!.Replace(" pt", ""), out var thickness)) return;
        if (CurrentShapeForStyle() is Shape shape)
            shape.StrokeThickness = thickness;
    }
}
