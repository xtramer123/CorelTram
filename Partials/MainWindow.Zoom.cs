using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VectorTram;

public partial class MainWindow
{
    // ========== ZOOM & VIEW ==========
    private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var factor = e.Delta > 0 ? 1.1 : 0.9;
            ZoomAt(e.GetPosition(PasteboardGrid), factor);
            e.Handled = true;
        }
    }

    private void ApplyZoom()
    {
        PasteboardGrid.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        UpdateSelectionChrome();
    }

    // Zooms keeping the given workspace point under the cursor.
    private void ZoomAt(Point workspacePoint, double factor)
    {
        double old = _zoom;
        double z = Math.Clamp(_zoom * factor, 0.05, 20.0);
        if (Math.Abs(z - old) < 1e-6) return;

        var offsetX = CanvasScrollViewer.HorizontalOffset;
        var offsetY = CanvasScrollViewer.VerticalOffset;

        _zoom = z;
        ApplyZoom();
        UpdateZoomUI();
        CanvasScrollViewer.UpdateLayout();

        CanvasScrollViewer.ScrollToHorizontalOffset(offsetX + workspacePoint.X * (z - old));
        CanvasScrollViewer.ScrollToVerticalOffset(offsetY + workspacePoint.Y * (z - old));
    }

    private void UpdateZoomUI()
    {
        ZoomText.Text = $"{(_zoom * 100):F0}%";

        if (ZoomCombo != null && !_isUpdatingUI)
        {
            _isUpdatingUI = true;
            var want = $"{Math.Round(_zoom * 100)}%";
            var match = ZoomCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => (i.Content?.ToString() ?? string.Empty) == want);
            if (match != null) ZoomCombo.SelectedItem = match;
            _isUpdatingUI = false;
        }
    }

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isUpdatingUI) return;
        if (ZoomCombo.SelectedItem is not ComboBoxItem item) return;

        var content = item.Content?.ToString() ?? string.Empty;
        if (content.Contains("Ajustar", StringComparison.OrdinalIgnoreCase))
        {
            FitToWindow_Click(sender, e);
            return;
        }

        if (double.TryParse(content.Replace("%", ""), out var pct) && pct > 0)
        {
            _zoom = Math.Clamp(pct / 100.0, 0.05, 20.0);
            ApplyZoom();
            UpdateZoomUI();
            CenterOnPage();
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        var center = new Point(CanvasScrollViewer.ViewportWidth / 2, CanvasScrollViewer.ViewportHeight / 2);
        ZoomAt(new Point(
            (CanvasScrollViewer.HorizontalOffset + center.X) / _zoom,
            (CanvasScrollViewer.VerticalOffset + center.Y) / _zoom), 1.2);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        var center = new Point(CanvasScrollViewer.ViewportWidth / 2, CanvasScrollViewer.ViewportHeight / 2);
        ZoomAt(new Point(
            (CanvasScrollViewer.HorizontalOffset + center.X) / _zoom,
            (CanvasScrollViewer.VerticalOffset + center.Y) / _zoom), 1 / 1.2);
    }

    private void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        double pad = 60;
        double availW = Math.Max(50, CanvasScrollViewer.ViewportWidth - pad);
        double availH = Math.Max(50, CanvasScrollViewer.ViewportHeight - pad);
        double z = Math.Min(availW / PaperBorder.Width, availH / PaperBorder.Height);
        _zoom = Math.Clamp(z, 0.05, 20.0);
        ApplyZoom();
        UpdateZoomUI();
        CenterOnPage();
    }

    private void CenterOnPage()
    {
        CanvasScrollViewer.UpdateLayout();
        double cx = (_pageOriginX + PaperBorder.Width / 2) * _zoom;
        double cy = (_pageOriginY + PaperBorder.Height / 2) * _zoom;
        CanvasScrollViewer.ScrollToHorizontalOffset(cx - CanvasScrollViewer.ViewportWidth / 2);
        CanvasScrollViewer.ScrollToVerticalOffset(cy - CanvasScrollViewer.ViewportHeight / 2);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterOnPage();
    }

    private void ToggleRulers_Click(object sender, RoutedEventArgs e)
    {
        bool show = !(sender is MenuItem mi) || mi.IsChecked;
        RulerRow.Height = show ? new GridLength(20) : new GridLength(0);
        RulerCol.Width = show ? new GridLength(20) : new GridLength(0);
        if (HorizontalRuler != null) HorizontalRuler.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (VerticalRuler != null) VerticalRuler.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleGrid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
        {
            // Toggle grid background on canvas
            if (mi.IsChecked)
            {
                DrawingCanvas.Background = CreateGridBrush();
            }
            else
            {
                DrawingCanvas.Background = Brushes.Transparent;
            }
        }
    }

    private DrawingBrush CreateGridBrush()
    {
        return new DrawingBrush
        {
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Drawing = new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new Rect(0, 0, 20, 20)),
                Brush = Brushes.Transparent,
                Pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), 0.5)
            }
        };
    }

    private void ToggleSnapToGrid_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Alinear a cuadrícula: No implementado aún";
    }
}
