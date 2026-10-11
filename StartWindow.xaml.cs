using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VectorTram;

public class ProjectCreatedEventArgs : EventArgs
{
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string FilePath { get; set; } = "";
}

public partial class StartWindow : Window
{
    public event EventHandler<ProjectCreatedEventArgs> ProjectCreated;
    
    // Results for App.xaml.cs
    public double? CreatedWidth { get; private set; }
    public double? CreatedHeight { get; private set; }
    public string CreatedFilePath { get; private set; } = "";
    
    private readonly string _recentFilesPath;
    private ObservableCollection<RecentProject> _recentProjects = new();
    private PaperSize _selectedPreset = null;
    private double _customWidth = 210;
    private double _customHeight = 297;
    private string _currentUnit = "mm";
    private bool _isPortrait = true;

    public StartWindow()
    {
        InitializeComponent();
        _recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VectorTram", "recent.json");
        Loaded += StartWindow_Loaded;
    }

    private void StartWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRecentProjects();
        InitializePresetSizes();
        UpdateCustomSizeFromPreset();
    }

    private void InitializePresetSizes()
    {
        var presets = new[]
        {
            new PaperSize("A4", 210, 297, "mm", "Estándar carta"),
            new PaperSize("A3", 297, 420, "mm", "Doble A4"),
            new PaperSize("A5", 148, 210, "mm", "Media A4"),
            new PaperSize("Letter", 215.9, 279.4, "mm", "US Letter (8.5×11\")"),
            new PaperSize("Legal", 215.9, 355.6, "mm", "US Legal (8.5×14\")"),
            new PaperSize("Tabloid", 279.4, 431.8, "mm", "US Tabloid (11×17\")"),
            new PaperSize("B4", 250, 353, "mm", "Estándar B"),
            new PaperSize("B5", 176, 250, "mm", "Media B4"),
        };

        foreach (var preset in presets)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("CardButtonStyle"),
                Margin = new Thickness(4),
                Padding = new Thickness(12),
                Tag = preset,
                ToolTip = preset.Description
            };
            btn.Click += PresetSize_Click;

            var stack = new StackPanel();
            var nameText = new TextBlock
            {
                Text = preset.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var dimText = new TextBlock
            {
                Text = $"{preset.Width} × {preset.Height} {preset.Unit}",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            var descText = new TextBlock
            {
                Text = preset.Description,
                FontSize = 10,
                Foreground = (Brush)FindResource("TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(nameText);
            stack.Children.Add(dimText);
            stack.Children.Add(descText);
            btn.Content = stack;
            PresetSizesPanel.Children.Add(btn);
        }

        // Select A4 by default
        if (PresetSizesPanel.Children.Count > 0)
        {
            SelectPreset((PresetSizesPanel.Children[0] as Button)?.Tag as PaperSize);
        }
    }

    private void SelectPreset(PaperSize preset)
    {
        _selectedPreset = preset;
        foreach (Button btn in PresetSizesPanel.Children)
        {
            if (btn.Tag is PaperSize p && p == preset)
            {
                btn.BorderBrush = (Brush)FindResource("AccentBrush");
                btn.BorderThickness = new Thickness(2);
            }
            else
            {
                btn.BorderBrush = (Brush)FindResource("BorderDefault");
                btn.BorderThickness = new Thickness(1);
            }
        }
        UpdateCustomSizeFromPreset();
    }

    private void UpdateCustomSizeFromPreset()
    {
        if (_selectedPreset != null)
        {
            _customWidth = _selectedPreset.Width;
            _customHeight = _selectedPreset.Height;
            _currentUnit = _selectedPreset.Unit;
            UpdateCustomSizeUI();
        }
    }

    private void UpdateCustomSizeUI()
    {
        CustomWidthText.Text = _customWidth.ToString("F1");
        CustomHeightText.Text = _customHeight.ToString("F1");
        UnitCombo.SelectedIndex = Array.IndexOf(new[] { "mm", "cm", "in", "px", "pt" }, _currentUnit);
        PortraitRadio.IsChecked = _isPortrait;
        LandscapeRadio.IsChecked = !_isPortrait;
    }

    private void LoadRecentProjects()
    {
        try
        {
            if (File.Exists(_recentFilesPath))
            {
                var json = File.ReadAllText(_recentFilesPath);
                var projects = System.Text.Json.JsonSerializer.Deserialize<List<RecentProject>>(json);
                if (projects != null)
                {
                    _recentProjects = new ObservableCollection<RecentProject>(projects.Take(10));
                }
            }
        }
        catch { }
        RecentProjectsList.ItemsSource = _recentProjects;
    }

    private void SaveRecentProjects()
    {
        try
        {
            var dir = Path.GetDirectoryName(_recentFilesPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(_recentProjects.ToList());
            File.WriteAllText(_recentFilesPath, json);
        }
        catch { }
    }

    private void AddRecentProject(string path)
    {
        var existing = _recentProjects.FirstOrDefault(p => p.Path == path);
        if (existing != null) _recentProjects.Remove(existing);

        var name = Path.GetFileNameWithoutExtension(path);
        _recentProjects.Insert(0, new RecentProject { Name = name, Path = path, Date = DateTime.Now.ToString("dd/MM/yyyy") });
        while (_recentProjects.Count > 10) _recentProjects.RemoveAt(_recentProjects.Count - 1);
        SaveRecentProjects();
    }

    private void PresetSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PaperSize preset)
        {
            SelectPreset(preset);
        }
    }

    private void CustomSize_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PresetSizesPanel == null || CustomWidthText == null || CustomHeightText == null) return;
        if (double.TryParse(CustomWidthText.Text, out var w)) _customWidth = w;
        if (double.TryParse(CustomHeightText.Text, out var h)) _customHeight = h;
        _selectedPreset = null;
        foreach (Button btn in PresetSizesPanel.Children)
        {
            btn.BorderBrush = (Brush)FindResource("BorderDefault");
            btn.BorderThickness = new Thickness(1);
        }
    }

    private void UnitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UnitCombo.SelectedItem is ComboBoxItem item && item.Tag is string unit)
        {
            _currentUnit = unit;
        }
    }

    private void Orientation_Changed(object sender, RoutedEventArgs e)
    {
        _isPortrait = PortraitRadio.IsChecked == true;
        if (_selectedPreset != null)
        {
            if (_isPortrait)
            {
                _customWidth = Math.Min(_selectedPreset.Width, _selectedPreset.Height);
                _customHeight = Math.Max(_selectedPreset.Width, _selectedPreset.Height);
            }
            else
            {
                _customWidth = Math.Max(_selectedPreset.Width, _selectedPreset.Height);
                _customHeight = Math.Min(_selectedPreset.Width, _selectedPreset.Height);
            }
            UpdateCustomSizeUI();
        }
    }

    private void NewProjectBtn_Click(object sender, RoutedEventArgs e)
    {
        ShowNewProjectView();
    }

    private void OpenProjectBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "VectorTram Files (*.vtm)|*.vtm|VectorTram (antiguo) (*.ctm)|*.ctm|XAML Files (*.xaml)|*.xaml|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            AddRecentProject(dialog.FileName);
            OpenMainWindow(dialog.FileName);
        }
    }

    private void RecentProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && File.Exists(path))
        {
            AddRecentProject(path);
            OpenMainWindow(path);
        }
    }

    private void MoreRecentBtn_Click(object sender, RoutedEventArgs e)
    {
        // Could open a full recent files dialog
    }

    private void QuickNewA4_Click(object sender, RoutedEventArgs e)
    {
        _customWidth = 210;
        _customHeight = 297;
        _currentUnit = "mm";
        _isPortrait = true;
        CreateNewDocument();
    }

    private void QuickCreateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null)
        {
            MessageBox.Show("Selecciona un tamaño de hoja primero", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CreateNewDocument();
    }

    private void ShowNewProjectView()
    {
        WelcomeScrollViewer.Visibility = Visibility.Collapsed;
        NewProjectScrollViewer.Visibility = Visibility.Visible;
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        WelcomeScrollViewer.Visibility = Visibility.Visible;
        NewProjectScrollViewer.Visibility = Visibility.Collapsed;
    }

    private void CreateProjectBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CreateNewDocument();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al crear proyecto: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateNewDocument()
    {
        // Convert to pixels (96 DPI)
        double widthPx, heightPx;
        switch (_currentUnit)
        {
            case "mm": widthPx = _customWidth * 96 / 25.4; heightPx = _customHeight * 96 / 25.4; break;
            case "cm": widthPx = _customWidth * 96 / 2.54; heightPx = _customHeight * 96 / 2.54; break;
            case "in": widthPx = _customWidth * 96; heightPx = _customHeight * 96; break;
            case "px": widthPx = _customWidth; heightPx = _customHeight; break;
            case "pt": widthPx = _customWidth * 96 / 72; heightPx = _customHeight * 96 / 72; break;
            default: widthPx = _customWidth * 96 / 25.4; heightPx = _customHeight * 96 / 25.4; break;
        }

        // Swap if landscape
        if (!_isPortrait)
        {
            var temp = widthPx;
            widthPx = heightPx;
            heightPx = temp;
        }

        OpenMainWindow(null, widthPx, heightPx);
    }

    public bool HasCreatedProject { get; private set; }

    private void OpenMainWindow(string filePath = null, double? width = null, double? height = null)
    {
        CreatedWidth = width;
        CreatedHeight = height;
        CreatedFilePath = filePath;
        HasCreatedProject = true;

        var mainWindow = new MainWindow(filePath, width, height);
        mainWindow.Show();
        Close();
    }
}

public class RecentProject
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Date { get; set; } = "";
}

public class PaperSize
{
    public string Name { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Unit { get; set; }
    public string Description { get; set; }

    public PaperSize(string name, double width, double height, string unit, string description)
    {
        Name = name;
        Width = width;
        Height = height;
        Unit = unit;
        Description = description;
    }
}