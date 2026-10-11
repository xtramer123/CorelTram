using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace CorelTram;

public partial class MainWindow
{
    // ========== LAYERS ==========
    private void CreateDefaultLayer()
    {
        _layers.Add(new LayerInfo { Name = "Capa 1", IsVisible = true, Elements = new ObservableCollection<UIElement>() });
    }

    private void UpdateLayersList()
    {
        LayersList.ItemsSource = null;
        LayersList.ItemsSource = _layers;
    }

    private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // Layer selection logic
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var layerName = $"Capa {_layers.Count + 1}";
        _layers.Add(new LayerInfo { Name = layerName, IsVisible = true, Elements = new ObservableCollection<UIElement>() });
        UpdateLayersList();
    }
}
