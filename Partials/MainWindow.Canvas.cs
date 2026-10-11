using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CorelTram;

public partial class MainWindow
{
    // ========== CANVAS EVENTS ==========
    private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(DrawingCanvas);
        _lastPoint = _startPoint;

        if (_currentTool == ToolMode.Select)
        {
            var hit = VisualTreeHelper.HitTest(DrawingCanvas, _startPoint);
            if (IsWithinThumb(hit?.VisualHit))
                return;
            if (hit?.VisualHit is FrameworkElement element && element != DrawingCanvas)
            {
                SelectElement(element);
                _isDragging = true;
                var left = Canvas.GetLeft(element);
                var top = Canvas.GetTop(element);
                _dragOffset = _startPoint - new Point(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top);
                DrawingCanvas.CaptureMouse();
                e.Handled = true;
            }
            else
            {
                DeselectElement();
            }
        }
        else if (_currentTool == ToolMode.Pan)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(CanvasScrollViewer);
            CanvasScrollViewer.CaptureMouse();
            CanvasScrollViewer.Cursor = Cursors.Hand;
        }
        else if (_currentTool == ToolMode.Zoom)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                ZoomAt(_startPoint, 1 / 1.3);
                return;
            }
            _isZoomDragging = true;
            _zoomStartPoint = _startPoint;
            DrawingCanvas.CaptureMouse();
            e.Handled = true;
        }
        else
        {
            _isDrawing = true;
            CreateShape(_startPoint);
            DrawingCanvas.CaptureMouse();
        }
    }

    private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(DrawingCanvas);
        CoordinateText.Text = $"X: {(int)currentPos.X}, Y: {(int)currentPos.Y}";

        if (_isDrawing && _currentShape != null)
        {
            UpdateShape(_currentShape, _startPoint, currentPos);
        }
        else if (_isDragging && _selectedElement != null)
        {
            var newPos = currentPos - _dragOffset;
            Canvas.SetLeft(_selectedElement, newPos.X);
            Canvas.SetTop(_selectedElement, newPos.Y);
            UpdatePropertiesPanel();
            UpdateSelectionChrome();
        }
        else if (_isPanning)
        {
            var current = e.GetPosition(CanvasScrollViewer);
            var offset = _panStartPoint - current;
            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset + offset.X);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset + offset.Y);
        }
        else if (_isZoomDragging)
        {
            UpdateMarquee(_zoomStartPoint, currentPos);
        }
    }

    private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            DrawingCanvas.ReleaseMouseCapture();
            if (_currentShape != null)
            {
                FinalizeShape(_currentShape);
                _currentShape = null;
            }
        }
        else if (_isDragging)
        {
            _isDragging = false;
            DrawingCanvas.ReleaseMouseCapture();
        }
        else if (_isPanning)
        {
            _isPanning = false;
            CanvasScrollViewer.ReleaseMouseCapture();
            CanvasScrollViewer.Cursor = Cursors.Arrow;
        }
        else if (_isZoomDragging)
        {
            _isZoomDragging = false;
            DrawingCanvas.ReleaseMouseCapture();
            var rect = RemoveMarquee();
            if (rect.Width < 8 || rect.Height < 8)
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                ZoomAt(_zoomStartPoint, shift ? 1 / 1.3 : 1.3);
            }
            else
            {
                ZoomToRect(rect);
            }
        }
    }

    private void DrawingCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDrawing || _isDragging || _isPanning || _isZoomDragging)
        {
            DrawingCanvas_MouseUp(sender, new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left));
        }
    }

    private void CanvasScrollViewer_MouseMove(object sender, MouseEventArgs e)
    {
        DrawingCanvas_MouseMove(sender, e);
    }

    // ========== ZOOM MARQUEE ==========
    private void UpdateMarquee(Point start, Point current)
    {
        if (_marqueeRect == null)
        {
            _marqueeRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),
                StrokeThickness = 1.0 / _zoom,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0x8C, 0x00)),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_marqueeRect, 1000001);
            SelectionLayer.Children.Add(_marqueeRect);
        }

        var r = new Rect(start, current);
        Canvas.SetLeft(_marqueeRect, r.Left);
        Canvas.SetTop(_marqueeRect, r.Top);
        _marqueeRect.Width = r.Width;
        _marqueeRect.Height = r.Height;
    }

    private Rect RemoveMarquee()
    {
        Rect r = Rect.Empty;
        if (_marqueeRect != null)
        {
            r = new Rect(Canvas.GetLeft(_marqueeRect), Canvas.GetTop(_marqueeRect), _marqueeRect.Width, _marqueeRect.Height);
            SelectionLayer.Children.Remove(_marqueeRect);
            _marqueeRect = null;
        }
        return r;
    }

    private void ZoomToRect(Rect rect)
    {
        double z = Math.Min(
            CanvasScrollViewer.ViewportWidth / rect.Width,
            CanvasScrollViewer.ViewportHeight / rect.Height);
        _zoom = Math.Clamp(z, 0.05, 20.0);
        ApplyZoom();
        UpdateZoomUI();
        CanvasScrollViewer.UpdateLayout();
        double cx = (rect.Left + rect.Width / 2) * _zoom;
        double cy = (rect.Top + rect.Height / 2) * _zoom;
        CanvasScrollViewer.ScrollToHorizontalOffset(cx - CanvasScrollViewer.ViewportWidth / 2);
        CanvasScrollViewer.ScrollToVerticalOffset(cy - CanvasScrollViewer.ViewportHeight / 2);
    }
}
