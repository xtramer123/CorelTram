using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CorelTram;

public partial class MainWindow
{
    // ========== TOOL SELECTION ==========
    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is not ToggleButton btn) return;
        if (_isUpdatingUI) return;

        _isUpdatingUI = true;
        foreach (var child in FindVisualChildren<ToggleButton>(this))
        {
            if (child != btn && child.Tag?.ToString() != null && Enum.TryParse<ToolMode>(child.Tag.ToString(), out _))
                child.IsChecked = false;
        }
        _isUpdatingUI = false;

        _currentTool = Enum.Parse<ToolMode>(btn.Tag.ToString()!);
        UpdateToolStatus();
        UpdateCursor();
        UpdateSelectionChrome();
    }

    private void Tool_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_isUpdatingUI) return;
        if (sender is ToggleButton btn && btn.Tag?.ToString() == _currentTool.ToString())
        {
            _isUpdatingUI = true;
            SelectToolBtn.IsChecked = true;
            _isUpdatingUI = false;
            _currentTool = ToolMode.Select;
            UpdateToolStatus();
            UpdateCursor();
        }
    }

    private void UpdateToolStatus()
    {
        ToolStatusText.Text = $"Herramienta: {_currentTool}";
    }

    private void UpdateCursor()
    {
        DrawingCanvas.Cursor = _currentTool switch
        {
            ToolMode.Select => Cursors.Arrow,
            ToolMode.Pan => Cursors.Hand,
            ToolMode.Zoom => Cursors.Cross,
            ToolMode.Pencil => Cursors.Pen,
            ToolMode.Brush => Cursors.Pen,
            _ => Cursors.Cross
        };
    }

    private void PanTool_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = ToolMode.Pan;
        UpdateToolStatus();
        UpdateCursor();
        // Uncheck all tool buttons
        foreach (var child in FindVisualChildren<ToggleButton>(this))
        {
            if (child.Tag?.ToString() != null && Enum.TryParse<ToolMode>(child.Tag.ToString(), out _))
                child.IsChecked = false;
        }
    }

    private void ZoomTool_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = ToolMode.Zoom;
        UpdateToolStatus();
        UpdateCursor();
        foreach (var child in FindVisualChildren<ToggleButton>(this))
        {
            if (child.Tag?.ToString() != null && Enum.TryParse<ToolMode>(child.Tag.ToString(), out _))
                child.IsChecked = false;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;

        ToggleButton? target = e.Key switch
        {
            Key.V => SelectToolBtn,
            Key.R => RectToolBtn,
            Key.E => EllipseToolBtn,
            Key.L => LineToolBtn,
            Key.T => TextToolBtn,
            Key.P => PencilToolBtn,
            Key.B => BrushToolBtn,
            _ => null
        };

        if (target != null)
        {
            if (target.IsChecked != true) target.IsChecked = true;
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.H:
                PanTool_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Z:
                ZoomTool_Click(sender, e);
                e.Handled = true;
                break;
        }
    }
}
