using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    // ========== PALETTE & GRADIENTS ==========
    private readonly List<(Border Border, Color Color)> _swatches = new();

    private void InitializeColorPalette()
    {
        var colors = new[]
        {
            "#000000", "#4A4A4A", "#8A8A8A", "#C9C9C9", "#FFFFFF",
            "#E4572E", "#FF6B6B", "#FF8C00", "#F6A609", "#FFE066",
            "#37B24D", "#1DD1A1", "#4ECDC4", "#0ABDE3", "#54A0FF",
            "#5F27CD", "#AA96DA", "#FCBAD3", "#F38181", "#95E1D3"
        };

        AddSwatch(Colors.Transparent);
        foreach (var hex in colors)
            AddSwatch((Color)ColorConverter.ConvertFromString(hex));

        UpdateColorIndicators();
    }

    private void AddSwatch(Color color)
    {
        AddSwatchTo(ColorPalette, color, 22);
        AddSwatchTo(ScreenPalette, color, 18);
    }

    private void AddSwatchTo(Panel host, Color color, double size)
    {
        var border = new Border
        {
            Style = (Style)FindResource("ColorSwatchStyle"),
            Width = size,
            Height = size,
            Background = new SolidColorBrush(color),
            Tag = color,
            ToolTip = color == Colors.Transparent
                ? "Sin color"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        };
        border.MouseLeftButtonDown += ColorSwatch_MouseLeftButtonDown;
        border.MouseRightButtonDown += ColorSwatch_MouseRightButtonDown;
        host.Children.Add(border);
        _swatches.Add((border, color));
    }

    private void InitializeGradients()
    {
        var gradients = new[]
        {
            new { Start = "#FF8C00", End = "#FF6B6B" },
            new { Start = "#4ECDC4", End = "#44A08D" },
            new { Start = "#FFE66D", End = "#FFB300" },
            new { Start = "#AA96DA", End = "#7B5FDB" },
            new { Start = "#F38181", End = "#F06292" },
            new { Start = "#54A0FF", End = "#007AFF" },
            new { Start = "#5F27CD", End = "#341F97" },
            new { Start = "#1DD1A1", End = "#10AC84" },
        };

        foreach (var g in gradients)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString(g.Start), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString(g.End), 1)
                }
            };

            var border = new Border
            {
                Width = 116,
                Height = 36,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Background = brush,
                BorderBrush = (Brush)FindResource("BorderSubtle"),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Tag = brush,
                ToolTip = "Clic izq.: trazo   ·   Clic der.: relleno"
            };
            border.MouseLeftButtonDown += GradientSwatch_MouseLeftButtonDown;
            border.MouseRightButtonDown += GradientSwatch_MouseRightButtonDown;
            GradientPalette.Children.Add(border);
        }
    }

    // ========== CURRENT COLORS ==========
    private Brush? CurrentStrokeBrush =>
        _strokeColor == Colors.Transparent ? null : new SolidColorBrush(_strokeColor);

    private Brush? CurrentFillBrush =>
        _fillColor == Colors.Transparent ? null : new SolidColorBrush(_fillColor);

    private void SetStrokeColor(Color color)
    {
        _strokeColor = color;
        var brush = color == Colors.Transparent ? null : (Brush)new SolidColorBrush(color);

        bool applied = false;
        foreach (var fe in _selection)
        {
            if (fe is Shape shape) { shape.Stroke = brush; applied = true; }
            else if (fe is TextBox tb) { tb.Foreground = brush ?? Brushes.Black; applied = true; }
        }
        if (!applied && _currentShape is Shape cs) cs.Stroke = brush;

        UpdateColorIndicators();
    }

    private void SetFillColor(Color color)
    {
        _fillColor = color;
        var brush = color == Colors.Transparent ? Brushes.Transparent : (Brush)new SolidColorBrush(color);

        bool applied = false;
        foreach (var fe in _selection)
        {
            if (fe is Shape shape) { shape.Fill = brush; applied = true; }
        }
        if (!applied && _currentShape is Shape cs) cs.Fill = brush;

        UpdateColorIndicators();
    }

    private void UpdateColorIndicators()
    {
        var strokeBrush = new SolidColorBrush(_strokeColor);
        var fillBrush = new SolidColorBrush(_fillColor);

        foreach (var b in new[] { StrokeColorIndicator, StrokeColorIndicatorLarge, StrokeColorIndicatorBar })
            if (b != null) b.Background = strokeBrush;
        foreach (var b in new[] { FillColorIndicator, FillColorIndicatorLarge, FillColorIndicatorBar })
            if (b != null) b.Background = fillBrush;

        RefreshPaletteSelection();
    }

    private void SyncColorsFromSelection(FrameworkElement element)
    {
        if (element is Shape shape)
        {
            if (shape.Stroke is SolidColorBrush sb) _strokeColor = sb.Color;
            else _strokeColor = Colors.Transparent;

            if (shape.Fill is SolidColorBrush fb) _fillColor = fb.Color;
            else _fillColor = Colors.Transparent;
        }
        else if (element is TextBox tb && tb.Foreground is SolidColorBrush tfb)
        {
            _strokeColor = tfb.Color;
        }

        UpdateColorIndicators();
    }

    private void RefreshPaletteSelection()
    {
        var accent = (Brush)FindResource("AccentBrush");
        var dark = (Brush)FindResource("BorderDark");

        foreach (var (border, color) in _swatches)
        {
            if (color == _strokeColor)
            {
                border.BorderBrush = accent;
                border.BorderThickness = new Thickness(2);
            }
            else
            {
                border.BorderBrush = color == Colors.Transparent ? dark : Brushes.Transparent;
                border.BorderThickness = new Thickness(1);
            }
        }
    }

    // ========== COLOR EVENTS ==========
    private void ColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Color color)
            SetStrokeColor(color);
    }

    private void ColorSwatch_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Color color)
            SetFillColor(color);
    }

    private void GradientSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Brush brush && _selectedElement is Shape shape)
            shape.Stroke = brush;
    }

    private void GradientSwatch_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Brush brush && _selectedElement is Shape shape)
            shape.Fill = brush;
    }

    private void MoreColors_Click(object sender, RoutedEventArgs e)
    {
        if (RightTabControl != null) RightTabControl.SelectedIndex = 2;
    }

    // ========== COLOR MIXER ==========
    private void Mixer_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixerRText == null || MixerGText == null || MixerBText == null || MixerPreview == null)
            return;

        var r = (byte)MixerRSlider.Value;
        var g = (byte)MixerGSlider.Value;
        var b = (byte)MixerBSlider.Value;
        var color = Color.FromRgb(r, g, b);

        MixerRText.Text = r.ToString();
        MixerGText.Text = g.ToString();
        MixerBText.Text = b.ToString();
        MixerPreview.Background = new SolidColorBrush(color);
    }

    private Color CurrentMixerColor()
    {
        if (MixerPreview?.Background is SolidColorBrush b) return b.Color;
        return Colors.Red;
    }

    private void ApplyMixerToStroke_Click(object sender, RoutedEventArgs e) => SetStrokeColor(CurrentMixerColor());

    private void ApplyMixerToFill_Click(object sender, RoutedEventArgs e) => SetFillColor(CurrentMixerColor());
}
