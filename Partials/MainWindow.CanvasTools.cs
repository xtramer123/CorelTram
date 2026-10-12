using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    // ========== HIT TESTING HELPERS ==========
    private FrameworkElement? HitTestByBounds(Point p)
    {
        FrameworkElement? found = null;
        foreach (var child in DrawingCanvas.Children)
        {
            if (child == SelectionLayer) continue;
            if (child is not FrameworkElement fe) continue;
            var b = GetElementBounds(fe);
            if (b.IsEmpty || b.Width <= 0 || b.Height <= 0) continue;
            if (b.Contains(p)) found = fe;
        }
        return found;
    }

    private FrameworkElement? FindTargetAt(Point p)
    {
        var hit = VisualTreeHelper.HitTest(DrawingCanvas, p);
        var target = FindCanvasChild(hit?.VisualHit);
        return target ?? HitTestByBounds(p);
    }

    // ========== FILL TOOL ==========
    private void ApplyInteractiveFill(Point p)
    {
        var target = FindTargetAt(p);
        if (target is Shape shape)
        {
            shape.Fill = CurrentFillBrush ?? Brushes.Transparent;
            SelectOnly(shape);
            StatusText.Text = "Relleno aplicado";
        }
        else if (target is TextBox tb)
        {
            tb.Foreground = CurrentStrokeBrush ?? Brushes.Black;
            StatusText.Text = "Color aplicado al texto";
        }
        else
        {
            StatusText.Text = "Relleno: haz clic sobre un objeto";
        }
    }

    // ========== EYEDROPPER TOOL ==========
    private void PickColorAt(Point p)
    {
        var target = FindTargetAt(p);
        bool picked = false;
        if (target is Shape shape)
        {
            if (shape.Fill is SolidColorBrush fb) { _fillColor = fb.Color; picked = true; }
            if (shape.Stroke is SolidColorBrush sb) { _strokeColor = sb.Color; picked = true; }
        }
        else if (target is TextBox tb && tb.Foreground is SolidColorBrush tbf)
        {
            _strokeColor = tbf.Color;
            picked = true;
        }

        if (picked)
        {
            UpdateColorIndicators();
            StatusText.Text = "Color tomado con el cuentagotas";
        }
        else
        {
            StatusText.Text = "Cuentagotas: haz clic sobre un objeto";
        }
    }

    // ========== OUTLINE (stroke) BUTTON ==========
    private void OutlineTool_Click(object sender, RoutedEventArgs e)
    {
        bool any = false;
        var thickness = CurrentStrokeThickness();
        foreach (var shape in ShapesToStyle())
        {
            shape.Stroke = CurrentStrokeBrush ?? Brushes.Black;
            shape.StrokeThickness = thickness;
            any = true;
        }
        StatusText.Text = any
            ? "Contorno aplicado a la selección"
            : "Contorno: selecciona primero un objeto";
    }

    // ========== CROP TOOL ==========
    private void CropPageTo(Rect rect)
    {
        var (originX, originY) = ComputePageOrigin(rect.Width, rect.Height);
        foreach (var child in DrawingCanvas.Children)
        {
            if (child == SelectionLayer) continue;
            if (child is not FrameworkElement fe) continue;
            double l = Canvas.GetLeft(fe); if (double.IsNaN(l)) l = 0;
            double t = Canvas.GetTop(fe); if (double.IsNaN(t)) t = 0;
            Canvas.SetLeft(fe, l - rect.Left + originX);
            Canvas.SetTop(fe, t - rect.Top + originY);
        }

        ClearSelection();
        SetPaperSize(rect.Width, rect.Height);
        StatusText.Text = $"Página recortada a {(int)rect.Width} × {(int)rect.Height} px";
    }

    // ========== SHAPE TOOL (node editing) ==========
    private void HandleShapeMouseDown(Point p)
    {
        var hit = VisualTreeHelper.HitTest(DrawingCanvas, p);
        if (IsWithinThumb(hit?.VisualHit)) return;

        var target = FindTargetAt(p);
        if (target != null)
        {
            if (!IsSelected(target)) SelectOnly(target);
        }
        else
        {
            ClearSelection();
        }
        UpdateNodeHandles();
    }

    private List<Point>? GetNodePoints(FrameworkElement fe)
    {
        double left = Canvas.GetLeft(fe); if (double.IsNaN(left)) left = 0;
        double top = Canvas.GetTop(fe); if (double.IsNaN(top)) top = 0;

        if (fe is Polyline poly)
        {
            var list = new List<Point>(poly.Points.Count);
            foreach (var pt in poly.Points) list.Add(new Point(left + pt.X, top + pt.Y));
            return list;
        }
        if (fe is System.Windows.Shapes.Polygon pg)
        {
            var list = new List<Point>(pg.Points.Count);
            foreach (var pt in pg.Points) list.Add(new Point(left + pt.X, top + pt.Y));
            return list;
        }
        if (fe is Line line)
            return new List<Point> { new(left + line.X1, top + line.Y1), new(left + line.X2, top + line.Y2) };
        if (fe is Rectangle rect)
            return RectCorners(left, top, ElemW(rect), ElemH(rect));
        if (fe is Ellipse ell)
            return RectCorners(left, top, ElemW(ell), ElemH(ell));
        if (fe is Path path && path.Tag is PenData pd)
        {
            var list = new List<Point>(pd.Nodes.Count);
            foreach (var n in pd.Nodes) list.Add(new Point(left + n.P.X, top + n.P.Y));
            return list;
        }
        return null;
    }

    private static double ElemW(FrameworkElement fe)
    {
        double w = fe.Width;
        if (double.IsNaN(w) || w <= 0) w = fe.ActualWidth;
        return Math.Max(w, 1);
    }

    private static double ElemH(FrameworkElement fe)
    {
        double h = fe.Height;
        if (double.IsNaN(h) || h <= 0) h = fe.ActualHeight;
        return Math.Max(h, 1);
    }

    private static List<Point> RectCorners(double left, double top, double w, double h) => new()
    {
        new Point(left, top),
        new Point(left + w, top),
        new Point(left + w, top + h),
        new Point(left, top + h)
    };

    private void RepositionNodeHandles()
    {
        if (_nodeTarget == null) return;
        var pts = GetNodePoints(_nodeTarget);
        if (pts == null) return;
        double size = HandleSize / _zoom;
        for (int i = 0; i < _nodeHandles.Count && i < pts.Count; i++)
        {
            Canvas.SetLeft(_nodeHandles[i], pts[i].X - size / 2);
            Canvas.SetTop(_nodeHandles[i], pts[i].Y - size / 2);
        }
    }

    private void UpdateNodeHandles()
    {
        foreach (var t in _nodeHandles) SelectionLayer.Children.Remove(t);
        _nodeHandles.Clear();
        _nodeTarget = null;

        if (_currentTool != ToolMode.Shape) return;
        var fe = SingleSelection;
        if (fe == null) return;

        var pts = GetNodePoints(fe);
        if (pts == null) return;

        _nodeTarget = fe;
        double size = HandleSize / _zoom;
        for (int i = 0; i < pts.Count; i++)
        {
            var thumb = new Thumb { Tag = i, Cursor = Cursors.SizeAll };
            thumb.Template = BuildNodeTemplate();
            thumb.Width = size;
            thumb.Height = size;
            Canvas.SetLeft(thumb, pts[i].X - size / 2);
            Canvas.SetTop(thumb, pts[i].Y - size / 2);
            thumb.DragDelta += NodeThumb_DragDelta;
            Panel.SetZIndex(thumb, 1000002);
            SelectionLayer.Children.Add(thumb);
            _nodeHandles.Add(thumb);
        }
    }

    private static ControlTemplate BuildNodeTemplate()
    {
        var rect = new FrameworkElementFactory(typeof(Rectangle));
        rect.SetValue(Rectangle.FillProperty, Brushes.White);
        rect.SetValue(Rectangle.StrokeProperty, new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
        rect.SetValue(Rectangle.StrokeThicknessProperty, 1.2);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = rect };
    }

    private void NodeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_nodeTarget == null || sender is not Thumb t || t.Tag is not int idx) return;

        var pos = Mouse.GetPosition(DrawingCanvas);
        double left = Canvas.GetLeft(_nodeTarget); if (double.IsNaN(left)) left = 0;
        double top = Canvas.GetTop(_nodeTarget); if (double.IsNaN(top)) top = 0;
        bool relayoutCorners = false;

        if (_nodeTarget is Polyline poly && idx >= 0 && idx < poly.Points.Count)
        {
            poly.Points[idx] = new Point(pos.X - left, pos.Y - top);
        }
        else if (_nodeTarget is System.Windows.Shapes.Polygon pg && idx >= 0 && idx < pg.Points.Count)
        {
            pg.Points[idx] = new Point(pos.X - left, pos.Y - top);
        }
        else if (_nodeTarget is Line line && (idx == 0 || idx == 1))
        {
            if (idx == 0) { line.X1 = pos.X - left; line.Y1 = pos.Y - top; }
            else { line.X2 = pos.X - left; line.Y2 = pos.Y - top; }
        }
        else if (_nodeTarget is Rectangle || _nodeTarget is Ellipse)
        {
            ResizeFromCorner(_nodeTarget, idx, pos);
            relayoutCorners = true;
        }
        else if (_nodeTarget is Path path && path.Tag is PenData pd && idx >= 0 && idx < pd.Nodes.Count)
        {
            pd.Nodes[idx].P = new Point(pos.X - left, pos.Y - top);
            path.Data = BuildGeometry(pd.Nodes, pd.Closed);
        }
        else
        {
            return;
        }

        if (relayoutCorners)
        {
            RepositionNodeHandles();
        }
        else
        {
            double size = HandleSize / _zoom;
            Canvas.SetLeft(t, pos.X - size / 2);
            Canvas.SetTop(t, pos.Y - size / 2);
        }

        _isUpdatingUI = true;
        UpdatePropertiesPanel();
        _isUpdatingUI = false;
        UpdateSelectionChrome();
    }

    private static void ResizeFromCorner(FrameworkElement fe, int idx, Point pos)
    {
        if (idx < 0 || idx > 3) return;
        double left = Canvas.GetLeft(fe); if (double.IsNaN(left)) left = 0;
        double top = Canvas.GetTop(fe); if (double.IsNaN(top)) top = 0;

        var corners = RectCorners(left, top, ElemW(fe), ElemH(fe));
        var anchor = corners[(idx + 2) % 4];

        double nx = Math.Min(anchor.X, pos.X);
        double ny = Math.Min(anchor.Y, pos.Y);
        double nw = Math.Max(Math.Abs(pos.X - anchor.X), 1);
        double nh = Math.Max(Math.Abs(pos.Y - anchor.Y), 1);

        Canvas.SetLeft(fe, nx);
        Canvas.SetTop(fe, ny);
        fe.Width = nw;
        fe.Height = nh;
    }

    // ========== RIGHT-CLICK CONTEXT MENU (move to layer) ==========
    private void DrawingCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == ToolMode.Pen || _currentTool == ToolMode.Zoom) return;

        var pos = e.GetPosition(DrawingCanvas);
        var target = FindTargetAt(pos);
        if (target == null) return;

        if (!IsSelected(target)) SelectOnly(target);

        var menu = new ContextMenu
        {
            PlacementTarget = DrawingCanvas,
            Placement = PlacementMode.MousePoint
        };

        var moveItem = new MenuItem { Header = "Mover a capa" };
        foreach (var layer in _layers)
        {
            var l = layer;
            var item = new MenuItem
            {
                Header = l.Name,
                IsCheckable = true,
                IsChecked = l.Elements.Contains(target)
            };
            item.Click += (s, args) => MoveElementToLayer(target, l);
            moveItem.Items.Add(item);
        }
        menu.Items.Add(moveItem);

        menu.Items.Add(new Separator());

        var front = new MenuItem { Header = "Traer al frente" };
        front.Click += (s, args) => BringToFront(target);
        menu.Items.Add(front);

        var back = new MenuItem { Header = "Enviar al fondo" };
        back.Click += (s, args) => SendToBack(target);
        menu.Items.Add(back);

        menu.Items.Add(new Separator());

        var del = new MenuItem { Header = "Eliminar" };
        del.Click += (s, args) =>
        {
            if (!IsSelected(target)) SelectOnly(target);
            DrawingCanvas.Children.Remove(target);
            foreach (var l in _layers) l.Elements.Remove(target);
            _selection.Remove(target);
            _selectedElement = null;
            UpdateLayersList();
            UpdateSelectionChrome();
            StatusText.Text = "Objeto eliminado";
        };
        menu.Items.Add(del);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void MoveElementToLayer(FrameworkElement fe, LayerInfo layer)
    {
        foreach (var l in _layers) l.Elements.Remove(fe);
        layer.Elements.Add(fe);
        fe.Visibility = layer.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayersList();
        StatusText.Text = $"Movido a \"{layer.Name}\"";
    }

    private void BringToFront(FrameworkElement fe)
    {
        Panel.SetZIndex(fe, DrawingCanvas.Children.Count);
        StatusText.Text = "Traído al frente";
    }

    private void SendToBack(FrameworkElement fe)
    {
        Panel.SetZIndex(fe, 0);
        StatusText.Text = "Enviado al fondo";
    }
}
