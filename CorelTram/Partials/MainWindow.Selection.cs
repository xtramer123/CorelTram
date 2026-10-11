using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace CorelTram;

public partial class MainWindow
{
    // ========== SELECTION CHROME (resize / rotate handles) ==========
    private void BuildSelectionChrome()
    {
        if (SelectionLayer == null) return;

        _selectionRect = new Rectangle
        {
            Stroke = (Brush)FindResource("AccentBrush"),
            StrokeThickness = 1,
            Fill = null,
            IsHitTestVisible = false
        };
        SelectionLayer.Children.Add(_selectionRect);

        var order = new[]
        {
            ResizeHandle.TopLeft, ResizeHandle.Top, ResizeHandle.TopRight, ResizeHandle.Right,
            ResizeHandle.BottomRight, ResizeHandle.Bottom, ResizeHandle.BottomLeft, ResizeHandle.Left
        };
        for (int i = 0; i < order.Length; i++)
        {
            var thumb = CreateResizeThumb(order[i]);
            _resizeThumbs[i] = thumb;
            SelectionLayer.Children.Add(thumb);
        }

        _rotateThumb = new Thumb { Tag = ResizeHandle.Rotate, Cursor = Cursors.Hand };
        _rotateThumb.Template = BuildRotateTemplate();
        _rotateThumb.DragStarted += ResizeThumb_DragStarted;
        _rotateThumb.DragDelta += ResizeThumb_DragDelta;
        SelectionLayer.Children.Add(_rotateThumb);

        SetSelectionChromeVisibility(Visibility.Collapsed);
    }

    private Thumb CreateResizeThumb(ResizeHandle handle)
    {
        var thumb = new Thumb { Tag = handle, Cursor = GetResizeCursor(handle) };

        var rect = new FrameworkElementFactory(typeof(Rectangle));
        rect.SetValue(Rectangle.FillProperty, Brushes.White);
        rect.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromRgb(0xE0, 0x6A, 0x00)));
        rect.SetValue(Rectangle.StrokeThicknessProperty, 1.0);
        rect.SetValue(Rectangle.RadiusXProperty, 1.0);
        rect.SetValue(Rectangle.RadiusYProperty, 1.0);
        thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = rect };

        thumb.DragStarted += ResizeThumb_DragStarted;
        thumb.DragDelta += ResizeThumb_DragDelta;
        return thumb;
    }

    private static ControlTemplate BuildRotateTemplate()
    {
        var grid = new FrameworkElementFactory(typeof(Grid));

        var ell = new FrameworkElementFactory(typeof(Ellipse));
        ell.SetValue(Ellipse.FillProperty, Brushes.White);
        ell.SetValue(Ellipse.StrokeProperty, new SolidColorBrush(Color.FromRgb(0xE0, 0x6A, 0x00)));
        ell.SetValue(Ellipse.StrokeThicknessProperty, 1.0);
        grid.AppendChild(ell);

        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetValue(TextBlock.TextProperty, "↻");
        glyph.SetValue(TextBlock.FontSizeProperty, 7.0);
        glyph.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xE0, 0x6A, 0x00)));
        glyph.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        glyph.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        grid.AppendChild(glyph);

        return new ControlTemplate(typeof(Thumb)) { VisualTree = grid };
    }

    private static Cursor GetResizeCursor(ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
        ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
        ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
        ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
        _ => Cursors.Arrow
    };

    private void SetSelectionChromeVisibility(Visibility visibility)
    {
        if (SelectionLayer != null)
            SelectionLayer.Visibility = visibility;
    }

    private Rect GetElementBounds(FrameworkElement fe)
    {
        double left = Canvas.GetLeft(fe);
        double top = Canvas.GetTop(fe);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        if (fe is Line line)
        {
            double ax1 = left + line.X1, ay1 = top + line.Y1;
            double ax2 = left + line.X2, ay2 = top + line.Y2;
            return new Rect(Math.Min(ax1, ax2), Math.Min(ay1, ay2), Math.Abs(ax2 - ax1), Math.Abs(ay2 - ay1));
        }

        if (fe is Polyline poly)
        {
            if (poly.Points.Count == 0) return Rect.Empty;
            double pMinX = poly.Points.Min(p => p.X);
            double pMaxX = poly.Points.Max(p => p.X);
            double pMinY = poly.Points.Min(p => p.Y);
            double pMaxY = poly.Points.Max(p => p.Y);
            return new Rect(left + pMinX, top + pMinY, pMaxX - pMinX, pMaxY - pMinY);
        }

        double w = (!double.IsNaN(fe.Width) && fe.Width > 0) ? fe.Width : fe.ActualWidth;
        double h = (!double.IsNaN(fe.Height) && fe.Height > 0) ? fe.Height : fe.ActualHeight;
        if (w <= 0) w = Math.Max(fe.ActualWidth, 1);
        if (h <= 0) h = Math.Max(fe.ActualHeight, 1);
        return new Rect(left, top, w, h);
    }

    private void UpdateSelectionChrome()
    {
        if (SelectionLayer == null) return;

        if (_selectedElement is not FrameworkElement fe || _currentTool != ToolMode.Select)
        {
            SetSelectionChromeVisibility(Visibility.Collapsed);
            return;
        }

        var b = GetElementBounds(fe);
        if (b.Width <= 0 || b.Height <= 0)
        {
            SetSelectionChromeVisibility(Visibility.Collapsed);
            return;
        }

        SetSelectionChromeVisibility(Visibility.Visible);

        double size = HandleSize / _zoom;
        double half = size / 2;

        _selectionRect!.StrokeThickness = 1.0 / _zoom;
        Canvas.SetLeft(_selectionRect, b.Left);
        Canvas.SetTop(_selectionRect, b.Top);
        _selectionRect.Width = b.Width;
        _selectionRect.Height = b.Height;

        double cx = b.Left + b.Width / 2;
        double cy = b.Top + b.Height / 2;
        double right = b.Left + b.Width;
        double bottom = b.Top + b.Height;

        PlaceHandle(_resizeThumbs[0], b.Left, b.Top, size, half);
        PlaceHandle(_resizeThumbs[1], cx, b.Top, size, half);
        PlaceHandle(_resizeThumbs[2], right, b.Top, size, half);
        PlaceHandle(_resizeThumbs[3], right, cy, size, half);
        PlaceHandle(_resizeThumbs[4], right, bottom, size, half);
        PlaceHandle(_resizeThumbs[5], cx, bottom, size, half);
        PlaceHandle(_resizeThumbs[6], b.Left, bottom, size, half);
        PlaceHandle(_resizeThumbs[7], b.Left, cy, size, half);

        if (_rotateThumb != null)
        {
            double rs = size * 1.5;
            _rotateThumb.Width = rs;
            _rotateThumb.Height = rs;
            Canvas.SetLeft(_rotateThumb, cx - rs / 2);
            Canvas.SetTop(_rotateThumb, b.Top - rs - 12 / _zoom);
        }
    }

    private static void PlaceHandle(Thumb? thumb, double centerX, double centerY, double size, double half)
    {
        if (thumb == null) return;
        thumb.Width = size;
        thumb.Height = size;
        Canvas.SetLeft(thumb, centerX - half);
        Canvas.SetTop(thumb, centerY - half);
    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (_selectedElement is not FrameworkElement fe) return;
        if (sender is Thumb t && t.Tag is ResizeHandle handle) _activeHandle = handle;
        _resizeBounds = GetElementBounds(fe);
        _lastMousePos = Mouse.GetPosition(DrawingCanvas);

        if (fe is Line line)
        {
            double left = Canvas.GetLeft(line); if (double.IsNaN(left)) left = 0;
            double top = Canvas.GetTop(line); if (double.IsNaN(top)) top = 0;
            double w = Math.Max(_resizeBounds.Width, 0.0001);
            double h = Math.Max(_resizeBounds.Height, 0.0001);
            _fracX1 = (left + line.X1 - _resizeBounds.Left) / w;
            _fracY1 = (top + line.Y1 - _resizeBounds.Top) / h;
            _fracX2 = (left + line.X2 - _resizeBounds.Left) / w;
            _fracY2 = (top + line.Y2 - _resizeBounds.Top) / h;
        }
        else if (fe is Polyline poly)
        {
            double left = Canvas.GetLeft(poly); if (double.IsNaN(left)) left = 0;
            double top = Canvas.GetTop(poly); if (double.IsNaN(top)) top = 0;
            double w = Math.Max(_resizeBounds.Width, 0.0001);
            double h = Math.Max(_resizeBounds.Height, 0.0001);
            _polyFractions = new List<Point>(poly.Points.Count);
            foreach (var p in poly.Points)
                _polyFractions.Add(new Point(
                    (left + p.X - _resizeBounds.Left) / w,
                    (top + p.Y - _resizeBounds.Top) / h));
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_selectedElement is not FrameworkElement fe) return;

        if (_activeHandle == ResizeHandle.Rotate)
        {
            RotateSelectedElement(fe);
            return;
        }

        double left = _resizeBounds.Left;
        double top = _resizeBounds.Top;
        double w = _resizeBounds.Width;
        double h = _resizeBounds.Height;

        bool leftSide = _activeHandle is ResizeHandle.TopLeft or ResizeHandle.Left or ResizeHandle.BottomLeft;
        bool rightSide = _activeHandle is ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight;
        bool topSide = _activeHandle is ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight;
        bool bottomSide = _activeHandle is ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight;

        var mousePos = Mouse.GetPosition(DrawingCanvas);
        double deltaX = mousePos.X - _lastMousePos.X;
        double deltaY = mousePos.Y - _lastMousePos.Y;
        _lastMousePos = mousePos;

        if (leftSide) { left += deltaX; w -= deltaX; }
        if (rightSide) { w += deltaX; }
        if (topSide) { top += deltaY; h -= deltaY; }
        if (bottomSide) { h += deltaY; }

        if (w < MinSize)
        {
            if (leftSide) left = _resizeBounds.Right - MinSize;
            w = MinSize;
        }
        if (h < MinSize)
        {
            if (topSide) top = _resizeBounds.Bottom - MinSize;
            h = MinSize;
        }

        _resizeBounds = new Rect(left, top, w, h);
        ApplyBounds(fe, _resizeBounds);
        UpdateSelectionChrome();

        _isUpdatingUI = true;
        UpdatePropertiesPanel();
        _isUpdatingUI = false;
    }

    private void ApplyBounds(FrameworkElement fe, Rect b)
    {
        if (fe is Line line)
        {
            double left = Canvas.GetLeft(line); if (double.IsNaN(left)) left = 0;
            double top = Canvas.GetTop(line); if (double.IsNaN(top)) top = 0;
            line.X1 = b.Left + _fracX1 * b.Width - left;
            line.Y1 = b.Top + _fracY1 * b.Height - top;
            line.X2 = b.Left + _fracX2 * b.Width - left;
            line.Y2 = b.Top + _fracY2 * b.Height - top;
        }
        else if (fe is Polyline poly && _polyFractions != null && _polyFractions.Count == poly.Points.Count)
        {
            Canvas.SetLeft(poly, b.Left);
            Canvas.SetTop(poly, b.Top);
            for (int i = 0; i < poly.Points.Count; i++)
            {
                var f = _polyFractions[i];
                poly.Points[i] = new Point(f.X * b.Width, f.Y * b.Height);
            }
        }
        else
        {
            Canvas.SetLeft(fe, b.Left);
            Canvas.SetTop(fe, b.Top);
            fe.Width = b.Width;
            fe.Height = b.Height;
        }
    }

    private void RotateSelectedElement(FrameworkElement fe)
    {
        var b = GetElementBounds(fe);
        double cx = b.Left + b.Width / 2;
        double cy = b.Top + b.Height / 2;
        var pos = Mouse.GetPosition(DrawingCanvas);
        double angle = Math.Atan2(pos.Y - cy, pos.X - cx) * 180.0 / Math.PI + 90;
        if (angle < 0) angle += 360;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            angle = Math.Round(angle / 15.0) * 15.0;

        fe.RenderTransformOrigin = new Point(0.5, 0.5);
        fe.RenderTransform = new RotateTransform(angle);

        _isUpdatingUI = true;
        RotationSlider.Value = angle;
        _isUpdatingUI = false;
        UpdateSelectionChrome();
    }

    // ========== SELECTION ==========
    private void SelectElement(FrameworkElement element)
    {
        DeselectElement();
        _selectedElement = element;

        if (element is Shape shape)
        {
            shape.Effect = (DropShadowEffect)FindResource("ShadowSmall");
            SyncColorsFromSelection(shape);
        }
        else if (element is TextBox tb)
        {
            tb.BorderThickness = new Thickness(1);
            tb.BorderBrush = (Brush)FindResource("AccentBrush");
            tb.Background = (Brush)FindResource("BgTeritary");
            SyncColorsFromSelection(tb);
        }
        else if (element is Image img)
        {
            img.Effect = (DropShadowEffect)FindResource("ShadowSmall");
        }

        UpdatePropertiesPanel();
        UpdateSelectionChrome();
        StatusText.Text = $"Seleccionado: {element.Name ?? "Objeto"}";
    }

    private void DeselectElement()
    {
        if (_selectedElement is Shape shape)
        {
            shape.Effect = null;
        }
        else if (_selectedElement is TextBox tb)
        {
            tb.BorderThickness = new Thickness(0);
            tb.Background = Brushes.Transparent;
        }
        else if (_selectedElement is Image img)
        {
            img.Effect = null;
        }
        _selectedElement = null;
        UpdatePropertiesPanel();
        SetSelectionChromeVisibility(Visibility.Collapsed);
    }
}
