using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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

    // Paper size presets in mm
    private static readonly Dictionary<string, (double w, double h)> PaperSizes = new()
    {
        ["A4"] = (210, 297),
        ["A3"] = (297, 420),
        ["A5"] = (148, 210),
        ["Letter"] = (215.9, 279.4),  // 8.5 x 11 in
        ["Oficio"] = (215.9, 355.6),  // 8.5 x 14 in
        ["Legal"] = (215.9, 355.6),   // 8.5 x 14 in
    };

    private void PaperSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (PaperSizeCombo?.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string tag) return;

        if (tag == "Custom")
        {
            // TODO: show custom size dialog
            StatusText.Text = "Tamaño personalizado: use el diálogo de configuración";
            return;
        }

        if (PaperSizes.TryGetValue(tag, out var size))
        {
            // Convert mm to pixels (96 DPI = 96/25.4 px per mm)
            const double mmToPx = 96.0 / 25.4;
            SetPaperSize(size.w * mmToPx, size.h * mmToPx);
            StatusText.Text = $"Tamaño de papel: {item.Content}";
        }
    }

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

    private void PrintPreview_Click(object sender, RoutedEventArgs e) => ShowPrintDialog(previewOnly: true);

    private void Print_Click(object sender, RoutedEventArgs e) => ShowPrintDialog(previewOnly: false);

    private void ShowPrintDialog(bool previewOnly)
    {
        // Create PrintDialog internally for print/preview capabilities, but DON'T show it
        var dlg = new PrintDialog
        {
            MinPage = 1,
            MaxPage = 1,
            PageRangeSelection = PageRangeSelection.AllPages,
            UserPageRangeEnabled = false
        };

        var printDlg = new PrintDialogWindow(this, dlg, previewOnly)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        printDlg.ShowDialog();

        if (printDlg.Printed)
            StatusText.Text = previewOnly ? "Vista previa cerrada" : "Documento enviado a la impresora";
    }

    // ========== CORELDRAW-STYLE PRINT DIALOG ==========
    private sealed class PrintDialogWindow : Window
    {
        private readonly MainWindow _owner;
        private readonly PrintDialog _printDialog;
        private bool _previewOnly;
        private DocumentViewer _docViewer;
        private FixedDocument _fixedDoc;

        public bool Printed { get; private set; }

        // Settings
        private string _selectedPrinter = "";
        private PageRangeSelection _pageRange = PageRangeSelection.AllPages;
        private string _customPageRange = "";
        private int _copies = 1;
        private bool _collate = true;
        private double _scale = 100;
        private bool _fitToPage = true;

        // UI refs
        private TextBox _scaleBox;

        public PrintDialogWindow(MainWindow owner, PrintDialog printDialog, bool previewOnly)
        {
            _owner = owner;
            _printDialog = printDialog;
            _previewOnly = previewOnly;
            _selectedPrinter = printDialog.PrintQueue?.Name ?? "";

            Title = previewOnly ? "Vista previa de impresión" : "Imprimir";
            Width = 1000;
            Height = 720;
            MinWidth = 900;
            MinHeight = 650;
            Background = (Brush)owner.FindResource("BgPanel");
            Foreground = (Brush)owner.FindResource("TextPrimary");
            FontFamily = new FontFamily("Segoe UI");

            BuildUI();
            BuildPreview();
        }

        private void BuildUI()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Content = grid;

            // LEFT PANEL - Options
            var leftPanel = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(16)
            };
            Grid.SetColumn(leftPanel, 0);

            var stack = new StackPanel();
            leftPanel.Content = stack;
            grid.Children.Add(leftPanel);

            // Printer section
            AddSectionHeader(stack, "Impresora");
            var printerCombo = new ComboBox
            {
                Margin = new Thickness(0, 4, 0, 12),
                Style = (Style)_owner.FindResource("PanelComboBoxStyle"),
                IsEditable = true
            };
            LoadPrinters(printerCombo);
            printerCombo.Text = _selectedPrinter;
            printerCombo.SelectionChanged += (s, e) => _selectedPrinter = printerCombo.Text;
            stack.Children.Add(printerCombo);

            // Copies
            var copiesPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            copiesPanel.Children.Add(new TextBlock { Text = "Copias:", VerticalAlignment = VerticalAlignment.Center, Width = 80 });
            var copiesBox = new TextBox { Text = "1", Width = 60, Style = (Style)_owner.FindResource("PanelTextBoxStyle"), Margin = new Thickness(0, 0, 12, 0) };
            copiesBox.TextChanged += (s, e) => { if (int.TryParse(copiesBox.Text, out var v) && v > 0) _copies = v; };
            var collateCheck = new CheckBox { Content = "Recopilar", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            collateCheck.Checked += (s, e) => _collate = true;
            collateCheck.Unchecked += (s, e) => _collate = false;
            copiesPanel.Children.Add(copiesBox);
            copiesPanel.Children.Add(collateCheck);
            stack.Children.Add(copiesPanel);

            // Page Range
            AddSectionHeader(stack, "Rango de páginas");
            var allPages = new RadioButton { Content = "Todas", IsChecked = true, GroupName = "PageRange", Margin = new Thickness(0, 2, 0, 2) };
            allPages.Checked += (s, e) => _pageRange = PageRangeSelection.AllPages;
            var currentPage = new RadioButton { Content = "Página actual", GroupName = "PageRange", Margin = new Thickness(0, 2, 0, 2) };
            currentPage.Checked += (s, e) => _pageRange = PageRangeSelection.CurrentPage;
            var selection = new RadioButton { Content = "Selección", GroupName = "PageRange", Margin = new Thickness(0, 2, 0, 2) };
            selection.Checked += (s, e) => _pageRange = PageRangeSelection.UserPages;
            var custom = new RadioButton { Content = "Páginas:", GroupName = "PageRange", Margin = new Thickness(0, 2, 0, 2) };
            custom.Checked += (s, e) => _pageRange = PageRangeSelection.UserPages;
            var customBox = new TextBox { Width = 180, Margin = new Thickness(60, 0, 0, 8), Style = (Style)_owner.FindResource("PanelTextBoxStyle"), ToolTip = "Ej: 1-3,5,7-9" };
            customBox.TextChanged += (s, e) => _customPageRange = customBox.Text;
            var rangePanel = new StackPanel();
            rangePanel.Children.Add(allPages);
            rangePanel.Children.Add(currentPage);
            rangePanel.Children.Add(selection);
            var customRow = new StackPanel { Orientation = Orientation.Horizontal };
            customRow.Children.Add(custom);
            customRow.Children.Add(customBox);
            rangePanel.Children.Add(customRow);
            stack.Children.Add(rangePanel);

            // Paper / Layout
            AddSectionHeader(stack, "Papel y diseño");
            stack.Children.Add(new TextBlock { Text = "Tamaño:", Margin = new Thickness(0, 0, 0, 4) });
            var paperCombo = new ComboBox
            {
                Margin = new Thickness(0, 4, 0, 8),
                Style = (Style)_owner.FindResource("PanelComboBoxStyle"),
                ItemsSource = new[] { "A4 (210×297 mm)", "A3 (297×420 mm)", "A5 (148×210 mm)", "Carta (8.5×11\")", "Oficio (8.5×14\")", "Legal (8.5×14\")", "Personalizado" }
            };
            paperCombo.SelectedIndex = 0;
            stack.Children.Add(paperCombo);

            var orientPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var portrait = new RadioButton { Content = "Vertical", IsChecked = true, GroupName = "Orient", Margin = new Thickness(0, 0, 16, 0) };
            var landscape = new RadioButton { Content = "Horizontal", GroupName = "Orient" };
            orientPanel.Children.Add(new TextBlock { Text = "Orientación:", VerticalAlignment = VerticalAlignment.Center, Width = 80 });
            orientPanel.Children.Add(portrait);
            orientPanel.Children.Add(landscape);
            stack.Children.Add(orientPanel);

            // Margins
            AddSectionHeader(stack, "Márgenes (mm)");
            var marginGrid = new Grid();
            marginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            marginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            marginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            marginGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mLeft = CreateMarginBox("Izq", 10);
            var mTop = CreateMarginBox("Sup", 10);
            var mRight = CreateMarginBox("Der", 10);
            var mBottom = CreateMarginBox("Inf", 10);
            Grid.SetColumn(mLeft, 0); Grid.SetColumn(mTop, 1); Grid.SetColumn(mRight, 2); Grid.SetColumn(mBottom, 3);
            marginGrid.Children.Add(mLeft); marginGrid.Children.Add(mTop); marginGrid.Children.Add(mRight); marginGrid.Children.Add(mBottom);
            stack.Children.Add(marginGrid);

            // Scale / Fit
            AddSectionHeader(stack, "Escala");
            var fitCheck = new CheckBox { Content = "Ajustar a página", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(fitCheck);
            var scalePanel = new StackPanel { Orientation = Orientation.Horizontal };
            scalePanel.Children.Add(new TextBlock { Text = "Escala personalizada:", VerticalAlignment = VerticalAlignment.Center, Width = 130 });
            _scaleBox = new TextBox { Text = "100", Width = 60, Style = (Style)_owner.FindResource("PanelTextBoxStyle"), Margin = new Thickness(0, 0, 4, 0), IsEnabled = false };
            _scaleBox.TextChanged += (s, e) => { if (double.TryParse(_scaleBox.Text, out var v) && v > 0) _scale = v; };
            scalePanel.Children.Add(_scaleBox);
            scalePanel.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(scalePanel);
            fitCheck.Checked += (s, e) => { _fitToPage = true; _scaleBox.IsEnabled = false; BuildPreview(); };
            fitCheck.Unchecked += (s, e) => { _fitToPage = false; _scaleBox.IsEnabled = true; BuildPreview(); };

            // Options
            AddSectionHeader(stack, "Opciones");
            stack.Children.Add(new CheckBox { Content = "Imprimir a archivo", Margin = new Thickness(0, 2, 0, 2) });
            stack.Children.Add(new CheckBox { Content = "Orden inverso", Margin = new Thickness(0, 2, 0, 2) });

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            var previewBtn = new Button { Content = "Vista previa", Width = 110, Height = 32, Margin = new Thickness(0, 0, 8, 0), Style = (Style)_owner.FindResource("PrimaryButtonStyle") };
            previewBtn.Click += (s, e) => { _previewOnly = true; BuildPreview(); };
            var printBtn = new Button { Content = _previewOnly ? "Cerrar" : "Imprimir", Width = 100, Height = 32, Margin = new Thickness(0, 0, 8, 0), Style = _previewOnly ? (Style)_owner.FindResource("GhostButtonStyle") : (Style)_owner.FindResource("PrimaryButtonStyle") };
            printBtn.Click += (s, e) => { if (!_previewOnly) DoPrint(); Close(); };
            var cancelBtn = new Button { Content = "Cancelar", Width = 90, Height = 32, Style = (Style)_owner.FindResource("GhostButtonStyle") };
            cancelBtn.Click += (s, e) => Close();
            if (!_previewOnly) btnPanel.Children.Add(printBtn);
            btnPanel.Children.Add(previewBtn);
            btnPanel.Children.Add(cancelBtn);
            stack.Children.Add(btnPanel);

            // SPLITTER
            var splitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Center, Background = (Brush)_owner.FindResource("BorderSubtle") };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            // PREVIEW AREA (right)
            var previewBorder = new Border
            {
                BorderBrush = (Brush)_owner.FindResource("BorderSubtle"),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Margin = new Thickness(8, 16, 16, 16)
            };
            Grid.SetColumn(previewBorder, 2);
            grid.Children.Add(previewBorder);

            _docViewer = new DocumentViewer { Zoom = 100 };
            previewBorder.Child = _docViewer;

            StackPanel CreateMarginBox(string label, double defaultVal)
            {
                var p = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 12, 0) };
                p.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = (Brush)_owner.FindResource("TextSecondary"), Margin = new Thickness(0, 0, 0, 2) });
                var box = new TextBox { Text = defaultVal.ToString(), Width = 50, Style = (Style)_owner.FindResource("PanelTextBoxStyle") };
                box.TextChanged += (s, e) => BuildPreview();
                p.Children.Add(box);
                return p;
            }

            void AddSectionHeader(Panel parent, string text)
            {
                parent.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)_owner.FindResource("TextPrimary"),
                    Margin = new Thickness(0, 16, 0, 4)
                });
            }

            void LoadPrinters(ComboBox combo)
            {
                try
                {
                    var server = new LocalPrintServer();
                    var queues = server.GetPrintQueues();
                    var names = new List<string>();
                    foreach (var q in queues) names.Add(q.Name);
                    combo.ItemsSource = names;
                }
                catch { combo.ItemsSource = new[] { _selectedPrinter }; }
            }
        }

        private void BuildPreview()
        {
            try
            {
                var pageSize = new Size(_printDialog.PrintableAreaWidth, _printDialog.PrintableAreaHeight);
                var visual = _owner.DrawingCanvas;

                var chromeVis = _owner.SelectionLayer.Visibility;
                _owner.SelectionLayer.Visibility = Visibility.Collapsed;

                var pageOrigin = new Point(_owner._pageOriginX, _owner._pageOriginY);
                var canvasWidth = Math.Max(_owner.PaperBorder.Width + PasteboardMargin * 2, 4000);
                var canvasHeight = Math.Max(_owner.PaperBorder.Height + PasteboardMargin * 2, 4000);

                double scaleFactor = _fitToPage ? 1.0 : (_scale / 100.0);

                var tg = new TransformGroup();
                if (!_fitToPage)
                    tg.Children.Add(new ScaleTransform(scaleFactor, scaleFactor, pageSize.Width / 2, pageSize.Height / 2));
                tg.Children.Add(new TranslateTransform(
                    -pageOrigin.X + (pageSize.Width - _owner.PaperBorder.Width * scaleFactor) / 2,
                    -pageOrigin.Y + (pageSize.Height - _owner.PaperBorder.Height * scaleFactor) / 2));

                visual.RenderTransform = tg;

                visual.Measure(pageSize);
                visual.Arrange(new Rect(0, 0, canvasWidth, canvasHeight));
                visual.UpdateLayout();

                _fixedDoc = new FixedDocument();
                var page = new FixedPage { Width = pageSize.Width, Height = pageSize.Height };
                var vb = new VisualBrush(visual) { Stretch = Stretch.None };
                var rect = new Rectangle { Width = canvasWidth, Height = canvasHeight, Fill = vb };
                page.Children.Add(rect);
                var pageContent = new PageContent { Child = page };
                _fixedDoc.Pages.Add(pageContent);

                _docViewer.Document = _fixedDoc;

                // Restore
                visual.RenderTransform = Transform.Identity;
                _owner.SelectionLayer.Visibility = chromeVis;
            }
            catch (Exception ex)
            {
                _owner.StatusText.Text = $"Error preview: {ex.Message}";
            }
        }

        private void DoPrint()
        {
            try
            {
                _printDialog.PrintDocument(_fixedDoc.DocumentPaginator, "VectorTram Document");
                Printed = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
