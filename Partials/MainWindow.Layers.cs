using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace VectorTram;

public partial class MainWindow
{
    // ========== LAYERS ==========
    private void CreateDefaultLayer()
    {
        AddLayerInternal("Capa 1");
    }

    private void RegisterLayer(LayerInfo layer)
    {
        layer.PropertyChanged += Layer_PropertyChanged;
    }

    private void Layer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LayerInfo layer) return;

        if (e.PropertyName == nameof(LayerInfo.IsVisible))
        {
            var visibility = layer.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            foreach (var el in layer.Elements)
            {
                if (el is UIElement ui) ui.Visibility = visibility;
            }
        }
        else if (e.PropertyName == nameof(LayerInfo.IsLocked) && layer.IsLocked)
        {
            var toRemove = layer.Elements.OfType<FrameworkElement>().Where(_selection.Contains).ToList();
            foreach (var fe in toRemove)
                RemoveFromSelection(fe);
        }
    }

    private LayerInfo AddLayerInternal(string name)
    {
        var layer = new LayerInfo { Name = name, IsVisible = true, Elements = new ObservableCollection<UIElement>() };
        RegisterLayer(layer);
        _layers.Add(layer);
        return layer;
    }

    private void UpdateLayersList()
    {
        var keep = LayersList.SelectedItem as LayerInfo;
        LayersList.ItemsSource = null;
        LayersList.ItemsSource = _layers;
        if (keep != null && _layers.Contains(keep))
            LayersList.SelectedItem = keep;
        else if (_layers.Count > 0)
            LayersList.SelectedItem = _layers[_layers.Count - 1];
    }

    private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // Layer selection logic
    }

    // ========== RENAME LAYER ==========
    private void LayersList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!IsLoaded) return;
        // Ignore double-clicks on the visibility checkbox / lock toggle.
        if (e.OriginalSource is DependencyObject d && FindLayerAncestor<ButtonBase>(d) != null) return;
        if (LayersList.SelectedItem is LayerInfo layer && !layer.IsEditing)
            BeginLayerRename(layer);
    }

    private static T? FindLayerAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null && obj is not T)
            obj = VisualTreeHelper.GetParent(obj);
        return obj as T;
    }

    private void BeginLayerRename(LayerInfo layer)
    {
        layer.EditName = layer.Name;
        layer.IsEditing = true;
        Dispatcher.BeginInvoke(new Action(() => FocusLayerEditBox(layer)),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FocusLayerEditBox(LayerInfo layer)
    {
        if (LayersList.ItemContainerGenerator.ContainerFromItem(layer) is not ListBoxItem container) return;
        var tb = FindVisualChildren<TextBox>(container).FirstOrDefault();
        if (tb != null)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void LayerNameEdit_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is LayerInfo l && l.IsEditing)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void LayerNameEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not LayerInfo layer) return;
        if (e.Key == Key.Enter)
        {
            CommitLayerRename(layer);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            layer.IsEditing = false;
            e.Handled = true;
        }
    }

    private void LayerNameEdit_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is LayerInfo layer && layer.IsEditing)
            CommitLayerRename(layer);
    }

    private void CommitLayerRename(LayerInfo layer)
    {
        if (!layer.IsEditing) return;
        var name = (layer.EditName ?? string.Empty).Trim();
        if (name.Length > 0) layer.Name = name;
        layer.IsEditing = false;
        StatusText.Text = $"Capa renombrada: \"{layer.Name}\"";
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var layer = AddLayerInternal($"Capa {_layers.Count + 1}");
        UpdateLayersList();
        LayersList.SelectedItem = layer;
    }

    private void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if ((LayersList.SelectedItem as LayerInfo ?? _layers.LastOrDefault()) is not LayerInfo layer)
        {
            StatusText.Text = "Selecciona una capa para eliminar";
            return;
        }
        if (_layers.Count <= 1)
        {
            StatusText.Text = "Debe existir al menos una capa";
            return;
        }

        foreach (var el in layer.Elements.ToList())
        {
            DrawingCanvas.Children.Remove(el);
            if (el is FrameworkElement fe && _selection.Contains(fe))
                RemoveFromSelection(fe);
        }
        layer.Elements.Clear();
        _layers.Remove(layer);
        _selectedElement = _selection.Count > 0 ? _selection[_selection.Count - 1] : null;
        UpdateLayersList();
        UpdateSelectionChrome();
        UpdateNodeHandles();
        StatusText.Text = $"Capa \"{layer.Name}\" eliminada";
    }

    private void DuplicateLayer_Click(object sender, RoutedEventArgs e)
    {
        if ((LayersList.SelectedItem as LayerInfo ?? _layers.LastOrDefault()) is not LayerInfo src)
        {
            StatusText.Text = "Selecciona una capa para duplicar";
            return;
        }

        var copy = new LayerInfo { Name = $"{src.Name} (copia)", IsVisible = src.IsVisible };
        RegisterLayer(copy);

        foreach (var el in src.Elements.ToList())
        {
            var clone = CloneElement(el);
            if (clone == null) continue;
            if (clone is FrameworkElement cfe)
            {
                double l = Canvas.GetLeft(cfe); if (double.IsNaN(l)) l = 0;
                double t = Canvas.GetTop(cfe); if (double.IsNaN(t)) t = 0;
                Canvas.SetLeft(cfe, l + 20);
                Canvas.SetTop(cfe, t + 20);
                cfe.Visibility = copy.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            DrawingCanvas.Children.Add(clone);
            copy.Elements.Add(clone);
        }

        _layers.Add(copy);
        UpdateLayersList();
        LayersList.SelectedItem = copy;
        StatusText.Text = $"Capa duplicada: \"{copy.Name}\"";
    }

    private static UIElement? CloneElement(UIElement el)
    {
        try
        {
            var xaml = XamlWriter.Save(el);
            return XamlReader.Parse(xaml) as UIElement;
        }
        catch
        {
            return null;
        }
    }

    private bool IsElementLocked(FrameworkElement fe)
    {
        foreach (var layer in _layers)
            if (layer.IsLocked && layer.Elements.Contains(fe))
                return true;
        return false;
    }
}
