using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VectorTram;

public partial class MainWindow
{
    // ========== TEXT TOOLS ==========
    private void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (_currentTool != ToolMode.Select) return;
        if (_editingTextBox == tb) return;

        if (e.ClickCount >= 2)
        {
            EnterTextEdit(tb);
            e.Handled = true;
            return;
        }

        SelectOnly(tb);
        BeginDrag(e.GetPosition(DrawingCanvas));
        DrawingCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && _currentTool == ToolMode.Select)
        {
            EnterTextEdit(tb);
            e.Handled = true;
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && IsSelected(tb))
            UpdateSelectionChrome();
    }

    private void InitializeFontFamilies()
    {
        foreach (var family in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
        {
            FontFamilyCombo.Items.Add(new ComboBoxItem { Content = family.Source, Tag = family });
        }
        FontFamilyCombo.SelectedIndex = FontFamilyCombo.Items.Cast<ComboBoxItem>().ToList().FindIndex(i => i.Content.ToString()!.Contains("Segoe UI", StringComparison.OrdinalIgnoreCase));
        if (FontFamilyCombo.SelectedIndex < 0) FontFamilyCombo.SelectedIndex = 0;
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_currentTextBox != null && FontFamilyCombo.SelectedItem is ComboBoxItem item)
        {
            _currentTextBox.FontFamily = item.Tag as FontFamily ?? new FontFamily("Segoe UI");
        }
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_currentTextBox != null && FontSizeCombo.SelectedItem is ComboBoxItem item)
        {
            _currentTextBox.FontSize = double.Parse(item.Content.ToString()!.Replace(" pt", ""));
        }
    }

    // Allows typing a custom font size in the editable combo box.
    private void FontSizeCombo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyCustomFontSize();
            e.Handled = true;
        }
    }

    private void FontSizeCombo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ApplyCustomFontSize();
    }

    private void ApplyCustomFontSize()
    {
        if (!IsLoaded || _currentTextBox == null) return;
        var size = ParseFontSize(FontSizeCombo.Text);
        if (size > 0)
            _currentTextBox.FontSize = size;
    }

    private static double ParseFontSize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var cleaned = text.Replace("pt", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(",", ".");
        return double.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0 ? s : 0;
    }

    private void TextAlignmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_currentTextBox != null && TextAlignmentCombo.SelectedItem is ComboBoxItem item && item.Tag is string align)
        {
            _currentTextBox.TextAlignment = Enum.Parse<TextAlignment>(align);
        }
    }

    private void TextStyle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_currentTextBox == null) return;

        _currentTextBox.FontWeight = BoldCheck.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        _currentTextBox.FontStyle = ItalicCheck.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
        _currentTextBox.TextDecorations = UnderlineCheck.IsChecked == true ? TextDecorations.Underline : null;
    }

    private void LineHeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LineHeightText == null) return;
        LineHeightText.Text = e.NewValue.ToString("F1");
        // LineHeight not directly supported in TextBox, would need RichTextBox
    }

    private void CharSpacing_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CharSpacingText == null) return;
        CharSpacingText.Text = ((int)e.NewValue).ToString();
        // Character spacing not directly supported in WPF TextBox
    }
}
