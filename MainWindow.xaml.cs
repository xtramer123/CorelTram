using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow : Window
{
    private enum ToolMode
    {
        Select,
        Rectangle,
        Ellipse,
        Line,
        Text,
        Pencil,
        Brush,
        Pen,
        Pan,
        Zoom,
        Shape,
        Crop,
        Polygon,
        Star,
        Fill,
        Eyedropper
    }

    private ToolMode _currentTool = ToolMode.Select;
    private Point _startPoint;
    private Point _lastPoint;
    private UIElement? _currentShape;
    private TextBox? _currentTextBox;
    private UIElement? _selectedElement;
    private bool _isDrawing = false;
    private bool _isDragging = false;
    private bool _isPanning = false;
    private Point _panStartPoint;
    private Point _dragStart;
    private readonly Dictionary<FrameworkElement, Point> _dragOrigins = new();
    private readonly List<FrameworkElement> _selection = new();
    private TextBox? _editingTextBox;
    private bool _isSelecting = false;
    private Point _selectStart;
    private Rectangle? _selectMarqueeRect;
    private bool _isZoomDragging = false;
    private Point _zoomStartPoint;
    private Rectangle? _marqueeRect;
    private bool _isCropping = false;
    private Point _cropStart;
    private readonly List<Thumb> _nodeHandles = new();
    private FrameworkElement? _nodeTarget;
    private double _zoom = 1.0;
    private readonly List<PenNode> _penNodes = new();
    private System.Windows.Shapes.Path? _penPath;
    private bool _penBuilding = false;
    private bool _penDraggingHandle = false;
    private bool _penClosed = false;
    private int _penActiveIndex = -1;
    private readonly ObservableCollection<LayerInfo> _layers = new();
    private int _objectCounter = 0;
    private Color _strokeColor = Colors.Black;
    private Color _fillColor = Colors.Transparent;
    private bool _isUpdatingUI = false;
    private int _tabCounter = 0;

    // Commands for keyboard shortcuts
    public static readonly RoutedCommand PrintCommand = new RoutedCommand("Print", typeof(MainWindow));
    public static readonly RoutedCommand NewFileCommand = new RoutedCommand("NewFile", typeof(MainWindow));
    public static readonly RoutedCommand OpenFileCommand = new RoutedCommand("OpenFile", typeof(MainWindow));
    public static readonly RoutedCommand SaveFileCommand = new RoutedCommand("SaveFile", typeof(MainWindow));
    public static readonly RoutedCommand UndoCommand = new RoutedCommand("Undo", typeof(MainWindow));
    public static readonly RoutedCommand RedoCommand = new RoutedCommand("Redo", typeof(MainWindow));
    public static readonly RoutedCommand SelectAllCommand = new RoutedCommand("SelectAll", typeof(MainWindow));
    public static readonly RoutedCommand DuplicateCommand = new RoutedCommand("Duplicate", typeof(MainWindow));
    public static readonly RoutedCommand DeleteCommand = new RoutedCommand("Delete", typeof(MainWindow));

    private enum ResizeHandle
    {
        TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, Rotate
    }

    private sealed class PenNode
    {
        public Point P;
        public Vector In;
        public Vector Out;
    }

    private sealed class PenData
    {
        public List<PenNode> Nodes = new();
        public double BaseW = 1;
        public double BaseH = 1;
        public bool Closed;
    }

    private const double HandleSize = 9.0;
    private const double MinSize = 6.0;
    private const double PasteboardMargin = 300.0;

    // Origin of the page within DrawingCanvas coordinates. Kept centered in the
    // canvas so the free workspace is balanced on all sides.
    private double _pageOriginX = PasteboardMargin;
    private double _pageOriginY = PasteboardMargin;

    private Rectangle? _selectionRect;
    private readonly Thumb?[] _resizeThumbs = new Thumb?[8];
    private Thumb? _rotateThumb;
    private ResizeHandle _activeHandle = ResizeHandle.TopLeft;
    private Rect _resizeBounds;
    private Point _lastMousePos;
    private double _fracX1, _fracY1, _fracX2, _fracY2;
    private List<Point>? _polyFractions;

    public MainWindow() : this(null, null, null) { }

    public MainWindow(string filePath, double? width, double? height)
    {
        try
        {
            InitializeComponent();
            
            // Command bindings for keyboard shortcuts
            CommandBindings.Add(new CommandBinding(PrintCommand, (s, e) => Print_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(NewFileCommand, (s, e) => NewFile_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(OpenFileCommand, (s, e) => OpenFile_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(SaveFileCommand, (s, e) => SaveFile_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(UndoCommand, (s, e) => Undo_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(RedoCommand, (s, e) => Redo_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(SelectAllCommand, (s, e) => SelectAll_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(DuplicateCommand, (s, e) => Duplicate_Click(this, new RoutedEventArgs())));
            CommandBindings.Add(new CommandBinding(DeleteCommand, (s, e) => Delete_Click(this, new RoutedEventArgs())));
            
            InitializeColorPalette();
            InitializeGradients();
            InitializeStrokePresets();
            InitializeFontFamilies();
            CreateDefaultLayer();
            UpdateLayersList();
            SelectToolBtn.IsChecked = true;
            UpdateToolStatus();
            BuildSelectionChrome();

            SetPaperSize(width ?? 794, height ?? 1123);

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                LoadFile(filePath);
            }

            // Add initial document tab
            AddDocumentTab("Sin título 1", this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al inicializar MainWindow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ========== DOCUMENT TABS ==========
    private void AddDocumentTab(string title, MainWindow window)
    {
        _tabCounter++;
        var tab = new Button
        {
            Style = (Style)FindResource("DocumentTabStyle"),
            Content = title,
            Tag = window,
            MinWidth = 120,
            MaxWidth = 200
        };
        tab.Click += (s, e) => SwitchToTab(tab);

        if (tab.Template != null)
        {
            tab.ApplyTemplate();
            if (tab.Template.FindName("CloseBtn", tab) is Button closeBtn)
            {
                closeBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(tab);
                };
            }
        }

        DocumentTabsPanel.Children.Add(tab);
        SwitchToTab(tab);
    }

    private void SwitchToTab(Button tab)
    {
        foreach (Button t in DocumentTabsPanel.Children)
        {
            if (t == tab)
            {
                t.Background = (Brush)FindResource("PaperBrush");
                t.BorderBrush = (Brush)FindResource("AccentBrush");
                t.BorderThickness = new Thickness(2, 2, 2, 0);
                t.Focus();
            }
            else
            {
                t.Background = (Brush)FindResource("BgToolbar");
                t.BorderBrush = (Brush)FindResource("BorderSubtle");
                t.BorderThickness = new Thickness(1, 1, 1, 0);
            }
        }
    }

    private void CloseTab(Button tab)
    {
        if (DocumentTabsPanel.Children.Count <= 1) return;

        var wasSelected = tab.BorderBrush == (Brush)FindResource("AccentBrush");
        DocumentTabsPanel.Children.Remove(tab);

        if (wasSelected && DocumentTabsPanel.Children.Count > 0)
        {
            SwitchToTab((Button)DocumentTabsPanel.Children[DocumentTabsPanel.Children.Count - 1]);
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        var newWindow = new MainWindow();
        newWindow.Show();
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        // Close current tab/window
        Close();
    }

    private void NewWindow_Click(object sender, RoutedEventArgs e)
    {
        var newWindow = new MainWindow();
        newWindow.Show();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _selection.Clear();
        foreach (var child in DrawingCanvas.Children)
        {
            if (child is FrameworkElement fe && fe != SelectionLayer)
            {
                _selection.Add(fe);
            }
        }
        _selectedElement = _selection.Count > 0 ? _selection[0] : null;
        UpdatePropertiesPanel();
        UpdateSelectionChrome();
        StatusText.Text = _selection.Count > 0 ? $"{_selection.Count} objetos seleccionados" : "Nada seleccionado";
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_selection.Count == 0) return;

        var toDuplicate = _selection.ToList();
        _selection.Clear();

        foreach (var element in toDuplicate)
        {
            if (element is FrameworkElement fe)
            {
                var xaml = System.Windows.Markup.XamlWriter.Save(fe);
                using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml));
                var clone = System.Windows.Markup.XamlReader.Load(stream) as FrameworkElement;
                if (clone != null)
                {
                    var left = Canvas.GetLeft(fe);
                    var top = Canvas.GetTop(fe);
                    Canvas.SetLeft(clone, double.IsNaN(left) ? 10 : left + 10);
                    Canvas.SetTop(clone, double.IsNaN(top) ? 10 : top + 10);
                    _objectCounter++;
                    clone.Name = $"{fe.Name}_copy{_objectCounter}";
                    DrawingCanvas.Children.Add(clone);
                    AddToCurrentLayer(clone);
                    _selection.Add(clone);
                }
            }
        }

        _selectedElement = _selection.Count > 0 ? _selection[0] : null;
        UpdateLayersList();
        UpdatePropertiesPanel();
        UpdateSelectionChrome();
        StatusText.Text = $"{toDuplicate.Count} objeto(s) duplicado(s)";
    }

    // ========== HELPERS ==========
    private static bool IsWithinThumb(DependencyObject? obj)
    {
        while (obj != null && obj is not Thumb)
            obj = VisualTreeHelper.GetParent(obj);
        return obj is Thumb;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) yield return t;
            foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
        }
    }
}
