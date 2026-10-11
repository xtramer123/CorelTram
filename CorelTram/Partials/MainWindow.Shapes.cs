using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CorelTram;

public partial class MainWindow
{
    // ========== SHAPE CREATION ==========
    private void CreateShape(Point start)
    {
        var strokeBrush = CurrentStrokeBrush;
        var fillBrush = CurrentFillBrush;
        var thickness = double.Parse(((ComboBoxItem)StrokeThicknessCombo.SelectedItem).Content.ToString()!.Replace(" pt", ""));

        _currentShape = _currentTool switch
        {
            ToolMode.Rectangle => new Rectangle
            {
                Stroke = strokeBrush,
                Fill = fillBrush,
                StrokeThickness = thickness,
                RadiusX = 0,
                RadiusY = 0
            },
            ToolMode.Ellipse => new Ellipse
            {
                Stroke = strokeBrush,
                Fill = fillBrush,
                StrokeThickness = thickness
            },
            ToolMode.Line => new Line
            {
                Stroke = strokeBrush,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            },
            ToolMode.Pencil => new Polyline
            {
                Stroke = strokeBrush,
                StrokeThickness = Math.Max(thickness, 1),
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = null
            },
            ToolMode.Brush => new Polyline
            {
                Stroke = strokeBrush,
                StrokeThickness = Math.Max(thickness, 1) * 3,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = null
            },
            ToolMode.Text => CreateTextBox(start),
            _ => null
        };

        if (_currentShape != null)
        {
            if (_currentTool is ToolMode.Pencil or ToolMode.Brush && _currentShape is Polyline poly)
            {
                Canvas.SetLeft(poly, 0);
                Canvas.SetTop(poly, 0);
                poly.Points.Add(start);
            }
            else if (_currentTool != ToolMode.Text)
            {
                Canvas.SetLeft(_currentShape, start.X);
                Canvas.SetTop(_currentShape, start.Y);
            }
            if (_currentShape is Shape styleShape && _currentTool != ToolMode.Text)
                ApplyStrokeStyleTo(styleShape);

            DrawingCanvas.Children.Add(_currentShape);
        }
    }

    private TextBox CreateTextBox(Point start)
    {
        var textBox = new TextBox
        {
            FontFamily = (FontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag as FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = double.Parse(((ComboBoxItem)FontSizeCombo.SelectedItem).Content.ToString()!.Replace(" pt", "")),
            Foreground = CurrentStrokeBrush ?? Brushes.Black,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinWidth = 120,
            MinHeight = 32,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            CaretBrush = (Brush)FindResource("AccentBrush"),
            SelectionBrush = (Brush)FindResource("AccentBrush"),
            SelectionTextBrush = (Brush)FindResource("PasteboardBrush")
        };

        if (TextAlignmentCombo.SelectedItem is ComboBoxItem alignItem && alignItem.Tag is string align)
        {
            textBox.TextAlignment = Enum.Parse<TextAlignment>(align);
        }

        Canvas.SetLeft(textBox, start.X);
        Canvas.SetTop(textBox, start.Y);
        textBox.Focus();
        _currentTextBox = textBox;
        return textBox;
    }

    private void UpdateShape(UIElement element, Point start, Point end)
    {
        if (element is not Shape shape) return;

        switch (shape)
        {
            case Rectangle rect:
                var rectX = Math.Min(start.X, end.X);
                var rectY = Math.Min(start.Y, end.Y);
                rect.Width = Math.Max(Math.Abs(end.X - start.X), 1);
                rect.Height = Math.Max(Math.Abs(end.Y - start.Y), 1);
                Canvas.SetLeft(rect, rectX);
                Canvas.SetTop(rect, rectY);
                break;
            case Ellipse ellipse:
                var ellipseX = Math.Min(start.X, end.X);
                var ellipseY = Math.Min(start.Y, end.Y);
                ellipse.Width = Math.Max(Math.Abs(end.X - start.X), 1);
                ellipse.Height = Math.Max(Math.Abs(end.Y - start.Y), 1);
                Canvas.SetLeft(ellipse, ellipseX);
                Canvas.SetTop(ellipse, ellipseY);
                break;
            case Line line:
                line.X1 = start.X;
                line.Y1 = start.Y;
                line.X2 = end.X;
                line.Y2 = end.Y;
                Canvas.SetLeft(line, 0);
                Canvas.SetTop(line, 0);
                break;
            case Polyline poly:
                if (poly.Points.Count == 0 ||
                    Math.Abs(poly.Points[poly.Points.Count - 1].X - end.X) +
                    Math.Abs(poly.Points[poly.Points.Count - 1].Y - end.Y) > 1.5)
                {
                    poly.Points.Add(end);
                }
                break;
        }
    }

    private void FinalizeShape(UIElement element)
    {
        if (element is Shape shape && shape is not Line && shape is not Polyline
            && !double.IsNaN(shape.Width) && shape.Width < 5
            && !double.IsNaN(shape.Height) && shape.Height < 5)
        {
            DrawingCanvas.Children.Remove(shape);
            return;
        }

        if (element is Polyline polyline)
            NormalizePolyline(polyline);

        _objectCounter++;
        var fe = element as FrameworkElement;
        if (fe != null)
            fe.Name = $"Objeto{_objectCounter}";
        AddToCurrentLayer(element);
        UpdateLayersList();
        StatusText.Text = $"Creado: {fe?.Name ?? "Objeto"}";
    }

    private void AddToCurrentLayer(UIElement element)
    {
        var currentLayer = _layers.FirstOrDefault(l => l.IsVisible) ?? _layers.FirstOrDefault();
        currentLayer?.Elements.Add(element);
    }

    private static void NormalizePolyline(Polyline poly)
    {
        if (poly.Points.Count == 0) return;

        double minX = poly.Points.Min(p => p.X);
        double minY = poly.Points.Min(p => p.Y);
        for (int i = 0; i < poly.Points.Count; i++)
            poly.Points[i] = new Point(poly.Points[i].X - minX, poly.Points[i].Y - minY);

        Canvas.SetLeft(poly, minX);
        Canvas.SetTop(poly, minY);
    }
}
