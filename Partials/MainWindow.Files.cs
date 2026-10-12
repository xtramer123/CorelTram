using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VectorTram;

public partial class MainWindow
{
    // ========== FILE OPERATIONS ==========
    private static (double X, double Y) ComputePageOrigin(double widthPx, double heightPx)
    {
        double canvasW = Math.Max(widthPx + PasteboardMargin * 2, 4000);
        double canvasH = Math.Max(heightPx + PasteboardMargin * 2, 4000);
        return (Math.Round((canvasW - widthPx) / 2), Math.Round((canvasH - heightPx) / 2));
    }

    private void SetPaperSize(double widthPx, double heightPx)
    {
        double canvasW = Math.Max(widthPx + PasteboardMargin * 2, 4000);
        double canvasH = Math.Max(heightPx + PasteboardMargin * 2, 4000);
        (_pageOriginX, _pageOriginY) = ComputePageOrigin(widthPx, heightPx);

        if (PaperBorder != null)
        {
            PaperBorder.Width = widthPx;
            PaperBorder.Height = heightPx;
            PaperBorder.Margin = new Thickness(_pageOriginX, _pageOriginY, 0, 0);
        }
        if (DrawingCanvas != null)
        {
            DrawingCanvas.Width = canvasW;
            DrawingCanvas.Height = canvasH;
        }
        CanvasScrollViewer?.ScrollToHome();
    }

    private void LoadFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var canvas = System.Windows.Markup.XamlReader.Load(stream) as Canvas;
            if (canvas != null)
            {
                // Recover the page origin the document was saved with so objects
                // land on the page even though the page is now centered in the
                // workspace. Legacy files (no metadata) used PasteboardMargin.
                double srcOriginX = PasteboardMargin;
                double srcOriginY = PasteboardMargin;
                if (canvas.Tag is string tag)
                {
                    var parts = tag.Split(';');
                    if (parts.Length == 5 && parts[0] == "VT2" &&
                        double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var pw) &&
                        double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var ph) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var ox) &&
                        double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var oy))
                    {
                        SetPaperSize(pw, ph);
                        srcOriginX = ox;
                        srcOriginY = oy;
                    }
                }
                double dx = _pageOriginX - srcOriginX;
                double dy = _pageOriginY - srcOriginY;

                foreach (UIElement existing in DrawingCanvas.Children.Cast<UIElement>().ToList())
                {
                    if (existing == SelectionLayer) continue;
                    DrawingCanvas.Children.Remove(existing);
                }
                var children = new List<UIElement>();
                foreach (UIElement child in canvas.Children)
                {
                    if (child == SelectionLayer) continue;
                    if (child is FrameworkElement fe && fe.Name == "SelectionLayer") continue;
                    children.Add(child);
                }
                foreach (var child in children)
                {
                    if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                    {
                        double l = Canvas.GetLeft(child); if (double.IsNaN(l)) l = 0;
                        double t = Canvas.GetTop(child); if (double.IsNaN(t)) t = 0;
                        Canvas.SetLeft(child, l + dx);
                        Canvas.SetTop(child, t + dy);
                    }
                    DrawingCanvas.Children.Add(child);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        var start = new StartWindow { Owner = this };
        start.ShowDialog();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "VectorTram Files (*.vtm)|*.vtm|VectorTram (antiguo) (*.ctm)|*.ctm|XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            LoadFile(dialog.FileName);
            StatusText.Text = $"Abierto: {dialog.FileName}";
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "VectorTram Files (*.vtm)|*.vtm|XAML Files (*.xaml)|*.xaml"
        };
        if (dialog.ShowDialog() == true)
        {
            SaveToXaml(dialog.FileName);
            StatusText.Text = $"Guardado: {dialog.FileName}";
        }
    }

    private void SaveAsFile_Click(object sender, RoutedEventArgs e) => SaveFile_Click(sender, e);

    private void SaveToXaml(string filename)
    {
        try
        {
            DrawingCanvas.Tag = string.Format(CultureInfo.InvariantCulture, "VT2;{0};{1};{2};{3}",
                _pageOriginX, _pageOriginY, PaperBorder.Width, PaperBorder.Height);
            using var stream = File.Create(filename);
            System.Windows.Markup.XamlWriter.Save(DrawingCanvas, stream);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PortraitPage_Click(object sender, RoutedEventArgs e) => SetPageOrientation(false);

    private void LandscapePage_Click(object sender, RoutedEventArgs e) => SetPageOrientation(true);

    // Swaps the page orientation. The page keeps its size, only its width and
    // height are exchanged, and the content is left untouched (like CorelDRAW).
    private void SetPageOrientation(bool landscape)
    {
        double w = PaperBorder.Width;
        double h = PaperBorder.Height;
        if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return;

        bool alreadyLandscape = w > h;
        if (alreadyLandscape != landscape)
            (w, h) = (h, w);

        SetPaperSize(w, h);
        CenterOnPage();
        StatusText.Text = landscape ? "Orientación: horizontal" : "Orientación: vertical";
    }

    private void ImportImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(dialog.FileName));
                var image = new Image
                {
                    Source = bitmap,
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                _objectCounter++;
                image.Name = $"Imagen{_objectCounter}";

                Canvas.SetLeft(image, 100);
                Canvas.SetTop(image, 100);

                DrawingCanvas.Children.Add(image);
                AddToCurrentLayer(image);
                UpdateLayersList();

                SelectOnly(image);

                StatusText.Text = $"Imagen importada: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Deshacer - No implementado aún";
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Rehacer - No implementado aún";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selection.Count > 0)
        {
            var toDelete = _selection.ToList();
            foreach (var fe in toDelete)
            {
                if (fe.Effect != null) fe.Effect = null;
                DrawingCanvas.Children.Remove(fe);
                foreach (var layer in _layers)
                    layer.Elements.Remove(fe);
            }
            _selection.Clear();
            _selectedElement = null;
            UpdateLayersList();
            UpdatePropertiesPanel();
            SetSelectionChromeVisibility(Visibility.Collapsed);
            StatusText.Text = toDelete.Count == 1 ? "Objeto eliminado" : $"{toDelete.Count} objetos eliminados";
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("VectorTram - Editor de Gráficos Vectoriales\nVersión 0.3\n\nDesarrollado en C# con WPF", "Acerca de", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
