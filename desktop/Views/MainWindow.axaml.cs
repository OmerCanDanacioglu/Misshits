using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Misshits.Desktop.ViewModels;

namespace Misshits.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        // Reclaim focus whenever a button is clicked
        AddHandler(Button.ClickEvent, OnAnyButtonClick, RoutingStrategies.Tunnel);

        // Grab focus once the window is opened
        Opened += (_, _) => Focus();
    }

    private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
    {
        // Return focus to the window so physical keyboard always works
        Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainWindowViewModel vm)
        {
            var code = MapKey(e.Key);
            if (code != null)
            {
                vm.Keyboard.HandlePhysicalKeyDown(code,
                    e.KeyModifiers.HasFlag(KeyModifiers.Control),
                    e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainWindowViewModel vm)
        {
            var code = MapKey(e.Key);
            if (code != null)
                vm.Keyboard.HandlePhysicalKeyUp(code);
        }
    }

    private static string? MapKey(Key key) => key switch
    {
        Key.A => "KeyA", Key.B => "KeyB", Key.C => "KeyC", Key.D => "KeyD",
        Key.E => "KeyE", Key.F => "KeyF", Key.G => "KeyG", Key.H => "KeyH",
        Key.I => "KeyI", Key.J => "KeyJ", Key.K => "KeyK", Key.L => "KeyL",
        Key.M => "KeyM", Key.N => "KeyN", Key.O => "KeyO", Key.P => "KeyP",
        Key.Q => "KeyQ", Key.R => "KeyR", Key.S => "KeyS", Key.T => "KeyT",
        Key.U => "KeyU", Key.V => "KeyV", Key.W => "KeyW", Key.X => "KeyX",
        Key.Y => "KeyY", Key.Z => "KeyZ",
        Key.D0 => "Digit0", Key.D1 => "Digit1", Key.D2 => "Digit2",
        Key.D3 => "Digit3", Key.D4 => "Digit4", Key.D5 => "Digit5",
        Key.D6 => "Digit6", Key.D7 => "Digit7", Key.D8 => "Digit8",
        Key.D9 => "Digit9",
        Key.Space => "Space",
        Key.Back => "Backspace",
        Key.Return => "Enter",
        Key.LeftShift => "ShiftLeft",
        Key.RightShift => "ShiftRight",
        Key.LeftCtrl => "ControlLeft",
        Key.RightCtrl => "ControlRight",
        Key.LeftAlt => "AltLeft",
        Key.RightAlt => "AltRight",
        Key.Capital => "CapsLock",
        Key.OemComma => "Comma",
        Key.OemPeriod => "Period",
        Key.Oem2 => "Slash",
        Key.OemMinus => "Minus",
        Key.OemPlus => "Equal",
        Key.Tab => "Tab",
        _ => null
    };
}
