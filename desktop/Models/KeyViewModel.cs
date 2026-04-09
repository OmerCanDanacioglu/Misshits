using CommunityToolkit.Mvvm.ComponentModel;

namespace Misshits.Desktop.Models;

public partial class KeyViewModel : ObservableObject
{
    public KeyDef Definition { get; }

    [ObservableProperty] private bool _isPressed;
    [ObservableProperty] private bool _isToggled;
    [ObservableProperty] private string _displayLabel;

    public string Code => Definition.Code;
    public double Width => Definition.Width;
    public bool Special => Definition.Special;
    public string? SubLabel => Definition.SubLabel;
    public bool IsSpace => Definition.Code == "Space";

    public bool IsActive => IsPressed || IsToggled;

    public KeyViewModel(KeyDef def)
    {
        Definition = def;
        _displayLabel = def.Label;
    }

    partial void OnIsPressedChanged(bool value) => OnPropertyChanged(nameof(IsActive));
    partial void OnIsToggledChanged(bool value) => OnPropertyChanged(nameof(IsActive));
}
