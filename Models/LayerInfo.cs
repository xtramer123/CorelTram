using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace VectorTram;

public class LayerInfo : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isVisible = true;
    private bool _isLocked = false;
    private bool _isEditing = false;
    private string _editName = "";
    private ObservableCollection<UIElement> _elements = new();

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }
    }

    public string EditName
    {
        get => _editName;
        set { _editName = value; OnPropertyChanged(nameof(EditName)); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); }
    }

    public ObservableCollection<UIElement> Elements
    {
        get => _elements;
        set { _elements = value; OnPropertyChanged(nameof(Elements)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
