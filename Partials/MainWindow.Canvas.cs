using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    // ========== CANVAS EVENTS ==========
    private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(DrawingCanvas);
        _lastPoint = _startPoint;

        // Middle mouse button always pans, whatever the active tool is.
        if (e.ChangedButton == MouseButton.Middle)
        {
            StartPan();
            DrawingCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_currentTool == ToolMode.Select)
        {
            var hit = VisualTreeHelper.HitTest(DrawingCanvas, _startPoint);
            if (IsWithinThumb(hit?.VisualHit))
                return;

            var target = FindCanvasChild(hit?.VisualHit);

            // Clicking inside the text box currently being edited -> let it place the caret.
            if (_editingTextBox != null && hit != null && IsDescendantOf(hit.VisualHit, _editingTextBox))
                return;

            if (target != null)
            {
                if (IsElementLocked(target))
                {
                    StatusText.Text = "Capa bloqueada";
                    e.Handled = true;
                    return;
                }
                if (_editingTextBox != null) ExitTextEdit();

                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (ctrl) ToggleSelection(target);
                else if (!IsSelected(target)) SelectOnly(target);

                if (IsSelected(target))
                {
                    BeginDrag(_startPoint);
                    DrawingCanvas.CaptureMouse();
                }
                e.Handled = true;
            }
            else
            {
                if (_editingTextBox != null) ExitTextEdit();
                ClearSelection();
                _isSelecting = true;
                _selectStart = _startPoint;
                DrawingCanvas.CaptureMouse();
                e.Handled = true;
            }
        }
        else if (_currentTool == ToolMode.Pan)
        {
            StartPan();
            DrawingCanvas.CaptureMouse();
            e.Handled = true;
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
        else if (_currentTool == ToolMode.Pen)
        {
            HandlePenMouseDown(_startPoint, e);
            e.Handled = true;
        }
        else if (_currentTool == ToolMode.Fill)
        {
            ApplyInteractiveFill(_startPoint);
            e.Handled = true;
        }
        else if (_currentTool == ToolMode.Eyedropper)
        {
            PickColorAt(_startPoint);
            e.Handled = true;
        }
        else if (_currentTool == ToolMode.Shape)
        {
            HandleShapeMouseDown(_startPoint);
            e.Handled = true;
        }
        else if (_currentTool == ToolMode.Crop)
        {
            _isCropping = true;
            _cropStart = _startPoint;
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
        else if (_isDragging && _selection.Count > 0)
        {
            var delta = currentPos - _dragStart;
            foreach (var kv in _dragOrigins)
            {
                Canvas.SetLeft(kv.Key, kv.Value.X + delta.X);
                Canvas.SetTop(kv.Key, kv.Value.Y + delta.Y);
            }
            UpdatePropertiesPanel();
            UpdateSelectionChrome();
        }
        else if (_isPanning)
        {
            var current = e.GetPosition(CanvasScrollViewer);
            var delta = _panStartPoint - current;
            _panStartPoint = current;
            CanvasScrollViewer.ScrollToHorizontalOffset(CanvasScrollViewer.HorizontalOffset + delta.X);
            CanvasScrollViewer.ScrollToVerticalOffset(CanvasScrollViewer.VerticalOffset + delta.Y);
        }
        else if (_isZoomDragging)
        {
            UpdateMarquee(_zoomStartPoint, currentPos);
        }
        else if (_isSelecting)
        {
            UpdateSelectionMarquee(_selectStart, currentPos);
        }
        else if (_isCropping)
        {
            UpdateMarquee(_cropStart, currentPos);
        }
        else if (_currentTool == ToolMode.Pen && _penBuilding)
        {
            UpdatePenPreview(currentPos, e.LeftButton == MouseButtonState.Pressed);
        }
    }

    private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == ToolMode.Pen && _penDraggingHandle)
        {
            _penDraggingHandle = false;
            DrawingCanvas.ReleaseMouseCapture();
            return;
        }

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
            _dragOrigins.Clear();
            DrawingCanvas.ReleaseMouseCapture();
        }
        else if (_isPanning)
        {
            _isPanning = false;
            DrawingCanvas.ReleaseMouseCapture();
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
        else if (_isSelecting)
        {
            _isSelecting = false;
            DrawingCanvas.ReleaseMouseCapture();
            var rect = RemoveSelectionMarquee();
            if (rect.Width >= 3 || rect.Height >= 3)
                SelectInRect(rect);
        }
        else if (_isCropping)
        {
            _isCropping = false;
            DrawingCanvas.ReleaseMouseCapture();
            var rect = RemoveMarquee();
            if (rect.Width >= 10 && rect.Height >= 10)
                CropPageTo(rect);
        }
    }

    private void DrawingCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DrawingCanvas.IsMouseCaptured) return;
        if (_isDrawing || _isDragging || _isPanning || _isZoomDragging || _isSelecting || _isCropping)
        {
            DrawingCanvas_MouseUp(sender, new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left));
        }
    }

    private void CanvasScrollViewer_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(DrawingCanvas);
        CoordinateText.Text = $"X: {(int)p.X}, Y: {(int)p.Y}";
    }

    private void StartPan()
    {
        _isPanning = true;
        _panStartPoint = Mouse.GetPosition(CanvasScrollViewer);
        CanvasScrollViewer.Cursor = Cursors.Hand;
    }

    private FrameworkElement? FindCanvasChild(DependencyObject? obj)
    {
        DependencyObject? child = null;
        while (obj != null && obj != DrawingCanvas)
        {
            child = obj;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return obj == DrawingCanvas ? child as FrameworkElement : null;
    }

    private static bool IsDescendantOf(DependencyObject? obj, DependencyObject ancestor)
    {
        while (obj != null && obj != ancestor)
            obj = VisualTreeHelper.GetParent(obj);
        return obj == ancestor;
    }

    // ========== SELECTION MARQUEE ==========
    private void UpdateSelectionMarquee(Point start, Point current)
    {
        if (_selectMarqueeRect == null)
        {
            _selectMarqueeRect = new Rectangle
            {
                Stroke = (Brush)FindResource("AccentBrush"),
                StrokeThickness = 1.0 / _zoom,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(28, 0xFF, 0x8C, 0x00)),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_selectMarqueeRect, 1000001);
            SelectionLayer.Children.Add(_selectMarqueeRect);
        }

        var r = new Rect(start, current);
        Canvas.SetLeft(_selectMarqueeRect, r.Left);
        Canvas.SetTop(_selectMarqueeRect, r.Top);
        _selectMarqueeRect.Width = r.Width;
        _selectMarqueeRect.Height = r.Height;
    }

    private Rect RemoveSelectionMarquee()
    {
        Rect r = Rect.Empty;
        if (_selectMarqueeRect != null)
        {
            r = new Rect(Canvas.GetLeft(_selectMarqueeRect), Canvas.GetTop(_selectMarqueeRect),
                _selectMarqueeRect.Width, _selectMarqueeRect.Height);
            SelectionLayer.Children.Remove(_selectMarqueeRect);
            _selectMarqueeRect = null;
        }
        return r;
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
