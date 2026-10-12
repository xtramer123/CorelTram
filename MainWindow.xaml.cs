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
