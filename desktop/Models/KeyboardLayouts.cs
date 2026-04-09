namespace Misshits.Desktop.Models;

public static class KeyboardLayouts
{
    public static readonly HashSet<string> ModifierCodes = new()
    {
        "ShiftLeft", "ShiftRight", "ControlLeft", "ControlRight", "AltLeft", "AltRight"
    };

    public static readonly KeyDef[][] Qwerty =
    [
        [
            new("Q", "KeyQ"), new("W", "KeyW"), new("E", "KeyE"), new("R", "KeyR"),
            new("T", "KeyT"), new("Y", "KeyY"), new("U", "KeyU"), new("I", "KeyI"),
            new("O", "KeyO"), new("P", "KeyP"),
            new("\u2190", "Backspace", 1.5, true, "backspace"),
        ],
        [
            new("A", "KeyA"), new("S", "KeyS"), new("D", "KeyD"), new("F", "KeyF"),
            new("G", "KeyG"), new("H", "KeyH"), new("J", "KeyJ"), new("K", "KeyK"),
            new("L", "KeyL"),
            new("\u21B5", "Enter", 1.5, true, "enter"),
        ],
        [
            new("\u21E7", "ShiftLeft", 1.2, true),
            new("Z", "KeyZ"), new("X", "KeyX"), new("C", "KeyC"), new("V", "KeyV"),
            new("B", "KeyB"), new("N", "KeyN"), new("M", "KeyM"),
            new(",", "Comma"), new(".", "Period"),
            new("\u2190", "BackspaceWord", 1.5, true, "delete word"),
        ],
        [
            new("123", "CapsLock", 1.2, true),
            new("!", "Digit1", 1.0, true), new("?", "Slash", 1.0, true),
            new("space", "Space", 8),
            new("Ctrl", "ControlLeft", 1.2, true), new("Alt", "AltLeft", 1.2, true),
        ],
    ];
}
