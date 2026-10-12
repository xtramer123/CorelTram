using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace VectorTram;

public partial class MainWindow
{
    // True while the Rectangle/Ellipse tool draws a perfect square/circle.
    private bool _constrainEqual;

    // ========== TOOL FLYOUTS (the small corner triangles) ==========
    private void ToolFlyout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        var items = btn.Tag?.ToString() switch
        {
            "Pencil" => new[]
            {
                (ToolMode.Pencil, "Lápiz", false),
                (ToolMode.Brush, "Pincel", false)
            },
            "Rectangle" => new[]
            {
                (ToolMode.Rectangle, "Rectángulo", false),
                (ToolMode.Rectangle, "Cuadrado", true)
            },
            "Ellipse" => new[]
            {
                (ToolMode.Ellipse, "Elipse", false),
                (ToolMode.Ellipse, "Círculo", true)
            },
            "Polygon" => new[]
            {
                (ToolMode.Polygon, "Polígono", false),
                (ToolMode.Star, "Estrella", false)
            },
            _ => null
        };

        if (items == null) return;

        OpenToolFlyout(btn, items);
        e.Handled = true;
    }

    private void OpenToolFlyout(Button anchor, (ToolMode mode, string label, bool equal)[] items)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };

        Popup? popup = null;
        foreach (var item in items)
        {
            var b = new Button
            {
                Content = item.label,
                MinWidth = 150,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 1, 0, 1)
            };
            var captured = item;
            b.Click += (s, args) =>
            {
                if (popup != null) popup.IsOpen = false;
                ChooseToolFromFlyout(captured.mode, captured.equal);
            };
            panel.Children.Add(b);
        }

        popup = new Popup
        {
            Child = new Border
            {
                Background = (Brush)FindResource("BgPanel"),
                BorderBrush = (Brush)FindResource("BorderDefault"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = panel
            },
            PlacementTarget = anchor,
            Placement = PlacementMode.Right,
            StaysOpen = false,
            AllowsTransparency = true
        };
        popup.IsOpen = true;
    }

    private void ChooseToolFromFlyout(ToolMode mode, bool equal)
    {
        ToggleButton? target = FindVisualChildren<ToggleButton>(this)
            .FirstOrDefault(t => t.Tag?.ToString() == mode.ToString());

        if (target != null && target.IsChecked != true)
        {
            target.IsChecked = true;
        }
        else
        {
            _currentTool = mode;
            UpdateToolStatus();
            UpdateCursor();
            UpdateNodeHandles();
            UpdateSelectionChrome();
        }

        _constrainEqual = equal;
        if (equal)
            StatusText.Text = mode == ToolMode.Ellipse ? "Círculo: arrastra para dibujar" : "Cuadrado: arrastra para dibujar";
    }
}
