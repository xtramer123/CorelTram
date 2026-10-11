using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CorelTram;

public partial class MainWindow
{
    // ========== TEXT TOOLS ==========
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
