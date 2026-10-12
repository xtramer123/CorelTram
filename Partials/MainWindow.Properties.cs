using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    // ========== PROPERTIES PANEL ==========
    private void UpdatePropertiesPanel()
    {
        _isUpdatingUI = true;
        if (_selectedElement is FrameworkElement fe)
        {
            ObjectNameText.Text = fe.Name ?? "";
            OpacitySlider.Value = fe.Opacity;
            if (fe.RenderTransform is RotateTransform rt)
                RotationSlider.Value = rt.Angle;
            else
                RotationSlider.Value = 0;

            var left = Canvas.GetLeft(fe);
            var top = Canvas.GetTop(fe);
            PosXText.Text = double.IsNaN(left) ? "0" : ((int)left).ToString();
            PosYText.Text = double.IsNaN(top) ? "0" : ((int)top).ToString();

            SizeWText.Text = double.IsNaN(fe.Width) || fe.Width == 0 ? "100" : ((int)fe.ActualWidth).ToString();
            SizeHText.Text = double.IsNaN(fe.Height) || fe.Height == 0 ? "100" : ((int)fe.ActualHeight).ToString();
        }
        else
        {
            ObjectNameText.Text = "";
            OpacitySlider.Value = 1;
            RotationSlider.Value = 0;
            PosXText.Text = "0";
            PosYText.Text = "0";
            SizeWText.Text = "100";
            SizeHText.Text = "100";
        }
        _isUpdatingUI = false;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe)
            fe.Opacity = e.NewValue;
    }

    private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe)
        {
            fe.RenderTransformOrigin = new Point(0.5, 0.5);
            fe.RenderTransform = new RotateTransform(e.NewValue);
            UpdateSelectionChrome();
        }
    }

    private void ObjectNameText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe)
            fe.Name = ObjectNameText.Text;
    }

    private void PosXText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe && double.TryParse(PosXText.Text, out var x))
        {
            Canvas.SetLeft(fe, x);
            UpdateSelectionChrome();
        }
    }

    private void PosYText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe && double.TryParse(PosYText.Text, out var y))
        {
            Canvas.SetTop(fe, y);
            UpdateSelectionChrome();
        }
    }

    private void SizeWText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe && double.TryParse(SizeWText.Text, out var w))
        {
            if (fe is Path path && path.Tag is PenData)
            {
                var b = GetElementBounds(fe);
                ApplyBounds(fe, new Rect(b.Left, b.Top, w, b.Height));
            }
            else fe.Width = w;
            UpdateSelectionChrome();
        }
    }

    private void SizeHText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!_isUpdatingUI && _selectedElement is FrameworkElement fe && double.TryParse(SizeHText.Text, out var h))
        {
            if (fe is Path path && path.Tag is PenData)
            {
                var b = GetElementBounds(fe);
                ApplyBounds(fe, new Rect(b.Left, b.Top, b.Width, h));
            }
            else fe.Height = h;
            UpdateSelectionChrome();
        }
    }
}
