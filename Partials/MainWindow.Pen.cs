using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    // ========== VECTOR PEN (BEZIER) ==========
    private Point _penLastCursor;
    private bool _penHasCursor;

    // While the pen is being built, pressing Shift straightens the active node
    // so an accidental curve becomes a straight segment without losing the path.
    // If we are mid-drag (creating the handles of the node we just placed) both
    // handles are cleared, cancelling that curve. If the mouse is up we only
    // clear the outgoing handle so the *next* segment is straight while the
    // curve already drawn (the incoming handle) is preserved.
    private void StraightenActivePenNode()
    {
        bool midDrag = _penDraggingHandle;
        if (_penActiveIndex >= 0 && _penActiveIndex < _penNodes.Count)
        {
            var n = _penNodes[_penActiveIndex];
            n.Out = new Vector(0, 0);
            if (midDrag)
                n.In = new Vector(0, 0);
        }
        RebuildPenGeometry(midDrag || !_penHasCursor ? null : _penLastCursor);
    }

    private void HandlePenMouseDown(Point p, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            FinishPenPath();
            return;
        }
        if (e.ChangedButton != MouseButton.Left) return;

        if (!_penBuilding)
        {
            StartPenPath(p);
            return;
        }

        if (e.ClickCount >= 2)
        {
            FinishPenPath();
            return;
        }

        if (_penNodes.Count >= 2 && (p - _penNodes[0].P).Length <= 12 / _zoom)
        {
            _penClosed = true;
            FinishPenPath();
            return;
        }

        _penNodes.Add(new PenNode { P = p });
        _penActiveIndex = _penNodes.Count - 1;
        _penDraggingHandle = true;
        DrawingCanvas.CaptureMouse();
        RebuildPenGeometry(null);
    }

    private void StartPenPath(Point p)
    {
        _penNodes.Clear();
        _penClosed = false;
        _penNodes.Add(new PenNode { P = p });
        _penActiveIndex = 0;
        _penBuilding = true;
        _penDraggingHandle = true;
        _penHasCursor = false;

        _penPath = new Path
        {
            Stroke = CurrentStrokeBrush ?? Brushes.Black,
            StrokeThickness = CurrentStrokeThickness(),
            Fill = null,
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ApplyStrokeStyleTo(_penPath);
        Panel.SetZIndex(_penPath, 900000);
        DrawingCanvas.Children.Add(_penPath);

        DrawingCanvas.CaptureMouse();
        RebuildPenGeometry(null);
        StatusText.Text = "Pluma: clic para añadir puntos, arrastra para curvar. Doble clic, Enter o clic derecho para terminar.";
    }

    private void UpdatePenPreview(Point cursor, bool pressed)
    {
        if (!_penBuilding || _penNodes.Count == 0) return;
        _penLastCursor = cursor;
        _penHasCursor = true;

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool activeValid = _penActiveIndex >= 0 && _penActiveIndex < _penNodes.Count;

        if (shift && activeValid)
        {
            var n = _penNodes[_penActiveIndex];
            n.Out = new Vector(0, 0);
            if (_penDraggingHandle)
                n.In = new Vector(0, 0);
        }

        if (pressed && _penDraggingHandle && activeValid && !shift)
        {
            var node = _penNodes[_penActiveIndex];
            var v = cursor - node.P;
            node.Out = v;
            node.In = new Vector(-v.X, -v.Y);
            RebuildPenGeometry(null);
        }
        else
        {
            RebuildPenGeometry(pressed ? null : cursor);
        }

        if (!pressed)
        {
            if (_penNodes.Count >= 2 && (cursor - _penNodes[0].P).Length <= 12 / _zoom)
                StatusText.Text = "Pluma: clic para cerrar la figura";
            else if (shift)
                StatusText.Text = "Pluma: segmento recto (Shift)";
            else
                StatusText.Text = "Pluma: clic para añadir puntos, arrastra para curvar. Doble clic, Enter o clic derecho para terminar.";
        }
    }

    private void RebuildPenGeometry(Point? cursor)
    {
        if (_penPath == null) return;
        var list = new List<PenNode>(_penNodes);
        if (cursor.HasValue && list.Count > 0)
            list.Add(new PenNode { P = cursor.Value });
        _penPath.Data = BuildGeometry(list, false);
        Canvas.SetLeft(_penPath, 0);
        Canvas.SetTop(_penPath, 0);
    }

    private void FinishPenPath()
    {
        _penDraggingHandle = false;

        if (!_penBuilding || _penNodes.Count < 2 || _penPath == null)
        {
            CancelPenPath();
            return;
        }

        double minX = _penNodes.Min(n => n.P.X);
        double minY = _penNodes.Min(n => n.P.Y);
        double maxX = _penNodes.Max(n => n.P.X);
        double maxY = _penNodes.Max(n => n.P.Y);

        var nodes = new List<PenNode>(_penNodes.Count);
        foreach (var n in _penNodes)
            nodes.Add(new PenNode
            {
                P = new Point(n.P.X - minX, n.P.Y - minY),
                In = n.In,
                Out = n.Out
            });

        var path = _penPath;
        path.Tag = new PenData
        {
            Nodes = nodes,
            BaseW = Math.Max(maxX - minX, 0.01),
            BaseH = Math.Max(maxY - minY, 0.01),
            Closed = _penClosed
        };
        path.Data = BuildGeometry(nodes, _penClosed);
        path.IsHitTestVisible = true;
        path.Stroke = CurrentStrokeBrush ?? path.Stroke;
        path.StrokeThickness = CurrentStrokeThickness();
        path.Fill = _penClosed ? CurrentFillBrush : null;
        ApplyStrokeStyleTo(path);
        Canvas.SetLeft(path, minX);
        Canvas.SetTop(path, minY);

        _penPath = null;
        _penNodes.Clear();
        _penBuilding = false;
        _penActiveIndex = -1;
        _penClosed = false;
        DrawingCanvas.ReleaseMouseCapture();

        FinalizeShape(path);
        StatusText.Text = "Trazado vectorial creado";
    }

    private void CancelPenPath()
    {
        if (_penPath != null)
            DrawingCanvas.Children.Remove(_penPath);
        _penPath = null;
        _penNodes.Clear();
        _penBuilding = false;
        _penDraggingHandle = false;
        _penActiveIndex = -1;
        _penClosed = false;
        _penHasCursor = false;
        if (DrawingCanvas.IsMouseCaptured) DrawingCanvas.ReleaseMouseCapture();
    }

    private double CurrentStrokeThickness()
    {
        if (StrokeThicknessCombo?.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Content.ToString()!.Replace(" pt", ""), out var t))
            return Math.Max(t, 1);
        return 1;
    }

    private static PathGeometry BuildGeometry(List<PenNode> nodes, bool closed)
    {
        var geo = new PathGeometry();
        if (nodes.Count == 0) return geo;

        var fig = new PathFigure { StartPoint = nodes[0].P, IsClosed = closed, IsFilled = closed };
        for (int i = 1; i < nodes.Count; i++)
            fig.Segments.Add(MakeSegment(nodes[i - 1], nodes[i]));
        if (closed && nodes.Count > 1)
            fig.Segments.Add(MakeSegment(nodes[nodes.Count - 1], nodes[0]));

        geo.Figures.Add(fig);
        return geo;
    }

    private static PathSegment MakeSegment(PenNode a, PenNode b)
    {
        bool straight = a.Out.LengthSquared < 0.01 && b.In.LengthSquared < 0.01;
        if (straight)
            return new LineSegment(b.P, true);

        var c1 = new Point(a.P.X + a.Out.X, a.P.Y + a.Out.Y);
        var c2 = new Point(b.P.X + b.In.X, b.P.Y + b.In.Y);
        return new BezierSegment(c1, c2, b.P, true);
    }
}
