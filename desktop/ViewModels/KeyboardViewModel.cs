using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Misshits.Desktop.Models;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.ViewModels;

public partial class KeyboardViewModel : ViewModelBase
{
    private readonly ISpellCheckService _spellCheck;
    private readonly ISmartConnectionService _smartConnection;
    private readonly ITextToSpeechService _tts;

    // --- Keyboard layout ---
    private static readonly HashSet<string> ModifierCodes = new()
    {
        "ShiftLeft", "ShiftRight", "ControlLeft", "ControlRight", "AltLeft", "AltRight"
    };

    private static readonly KeyDef[][] RowDefs =
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

    public List<List<KeyViewModel>> Rows { get; }

    // --- State ---
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _capsLock;
    [ObservableProperty] private bool _autoCorrectEnabled = true;
    [ObservableProperty] private bool _shorterOnly;
    [ObservableProperty] private bool _autoSpeak;
    [ObservableProperty] private bool _apiEnabled = true;
    [ObservableProperty] private bool _phrasesOpen;
    [ObservableProperty] private bool _correcting;
    [ObservableProperty] private string _currentWord = "";
    [ObservableProperty] private bool _cursorVisible = true;

    public string DisplayText => Text + (CursorVisible ? "|" : " ");

    public ObservableCollection<Suggestion> Suggestions { get; } = new();
    public ObservableCollection<string> Predictions { get; } = new();

    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasPredictions => Predictions.Count > 0 && Suggestions.Count == 0;

    private readonly HashSet<string> _pressedKeys = new();
    private readonly HashSet<string> _stickyMods = new();
    private readonly List<WordCorrection> _corrections = new();
    private readonly Stack<string> _textHistory = new();
    private const int MaxHistory = 50;

    private CancellationTokenSource? _spellCheckCts;
    private CancellationTokenSource? _predictionCts;

    public KeyboardViewModel(ISpellCheckService spellCheck, ISmartConnectionService smartConnection, ITextToSpeechService tts)
    {
        _spellCheck = spellCheck;
        _smartConnection = smartConnection;
        _tts = tts;

        // Build key view models from definitions
        Rows = RowDefs.Select(row =>
            row.Select(def => new KeyViewModel(def)).ToList()
        ).ToList();

        // Blinking cursor timer
        var cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        cursorTimer.Tick += (_, _) => CursorVisible = !CursorVisible;
        cursorTimer.Start();

        Suggestions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSuggestions));
            OnPropertyChanged(nameof(HasPredictions));
        };
        Predictions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPredictions));
    }

    // --- Text manipulation (with undo history) ---
    private void SetText(string newText)
    {
        if (newText == Text) return;
        if (_textHistory.Count >= MaxHistory) _textHistory.TrimExcess();
        _textHistory.Push(Text);
        Text = newText;
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
        ScheduleSpellCheck(value);
        SchedulePrediction(value);
        CheckSentenceCorrection(value);
    }

    partial void OnCursorVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    // --- Commands ---
    [RelayCommand]
    private void Speak()
    {
        if (!string.IsNullOrEmpty(Text))
            _tts.Speak(Text);
    }

    [RelayCommand]
    private void Clear() => SetText("");

    [RelayCommand]
    private void Undo()
    {
        if (_textHistory.Count > 0)
            Text = _textHistory.Pop(); // Direct set, don't push to history
    }

    [RelayCommand]
    private void TogglePhrases() => PhrasesOpen = !PhrasesOpen;

    [RelayCommand]
    private void ApplySuggestion(string term)
    {
        var match = Regex.Match(Text, @"[a-zA-Z]+$");
        if (!match.Success) return;
        _corrections.Add(new WordCorrection(match.Value, term));
        SetText(Text[..match.Index] + term);
    }

    [RelayCommand]
    private void InsertPrediction(string word)
    {
        var needsSpace = Text.Length > 0 && !Text.EndsWith(' ') && !Text.EndsWith('\n');
        SetText(Text + (needsSpace ? " " : "") + word + " ");
    }

    // --- Physical keyboard handling ---
    public void HandlePhysicalKeyDown(string code, bool ctrlKey, bool shiftKey)
    {
        _pressedKeys.Add(code);
        UpdateKeyStates();

        if (code == "CapsLock") { CapsLock = !CapsLock; UpdateKeyLabels(); return; }
        if (ModifierCodes.Contains(code)) return;

        if (code == "Backspace")
        {
            if (ctrlKey)
            {
                var trimmed = Text.TrimEnd();
                var lastWord = Regex.Match(trimmed, @"\S+$");
                SetText(lastWord.Success ? Text[..lastWord.Index] : "");
            }
            else
            {
                if (Text.Length > 0) SetText(Text[..^1]);
            }
            return;
        }

        if (code == "Enter") { SetText(Text + "\n"); return; }
        if (code == "Space") { AutoCorrectAndAppend(" "); ClearStickyMods(); return; }

        var ch = KeyToChar(code, shiftKey, CapsLock);
        if (ch != null)
        {
            if (".!?,;:'\"()-".Contains(ch.Value))
                AutoCorrectAndAppend(ch.Value.ToString());
            else
                SetText(Text + ch.Value);
        }
        ClearStickyMods();
    }

    public void HandlePhysicalKeyUp(string code)
    {
        _pressedKeys.Remove(code);
        UpdateKeyStates();
    }

    // --- Virtual keyboard click handling ---
    [RelayCommand]
    private void MouseClick(string code)
    {
        if (code == "CapsLock") { CapsLock = !CapsLock; UpdateKeyLabels(); return; }

        if (ModifierCodes.Contains(code))
        {
            if (_stickyMods.Contains(code)) _stickyMods.Remove(code);
            else _stickyMods.Add(code);
            UpdateKeyStates();
            return;
        }

        if (code == "Backspace")
        {
            if (Text.Length > 0) SetText(Text[..^1]);
            ClearStickyMods();
            return;
        }

        if (code == "BackspaceWord")
        {
            var trimmed = Text.TrimEnd();
            var lastWord = Regex.Match(trimmed, @"\S+$");
            SetText(lastWord.Success ? Text[..lastWord.Index] : "");
            ClearStickyMods();
            return;
        }

        if (code == "Enter") { SetText(Text + "\n"); ClearStickyMods(); return; }
        if (code == "Space") { AutoCorrectAndAppend(" "); ClearStickyMods(); return; }

        var shift = _stickyMods.Contains("ShiftLeft") || _stickyMods.Contains("ShiftRight");
        var ch = KeyToChar(code, shift, CapsLock);
        if (ch != null)
        {
            if (".!?,;:'\"()-".Contains(ch.Value))
                AutoCorrectAndAppend(ch.Value.ToString());
            else
                SetText(Text + ch.Value);
        }
        ClearStickyMods();
    }

    // --- Auto-correct ---
    private void AutoCorrectAndAppend(string suffix)
    {
        var match = Regex.Match(Text, @"[a-zA-Z]+$");
        if (!match.Success || !AutoCorrectEnabled)
        {
            SetText(Text + suffix);
            return;
        }

        var word = match.Value;

        // Check if current suggestions are for this word
        if (CurrentWord.Equals(word, StringComparison.OrdinalIgnoreCase)
            && Suggestions.Count > 0 && Suggestions[0].Distance > 0)
        {
            var corrected = Suggestions[0].Term;
            _corrections.Add(new WordCorrection(word, corrected));
            SetText(Text[..match.Index] + corrected + suffix);
            return;
        }

        // Suggestions stale or empty — fire deferred correction
        if (word.Length >= 2)
            _ = PerformDeferredCorrectionAsync(word, suffix);

        SetText(Text + suffix);
    }

    private async Task PerformDeferredCorrectionAsync(string word, string suffix)
    {
        try
        {
            var results = _spellCheck.Lookup(word);
            if (results.Count == 0 || results[0].Distance == 0) return;

            var corrected = results[0].Term;
            var pattern = word + suffix;

            Dispatcher.UIThread.Post(() =>
            {
                var idx = Text.LastIndexOf(pattern, StringComparison.Ordinal);
                if (idx == -1) return;
                _corrections.Add(new WordCorrection(word, corrected));
                SetText(Text[..idx] + corrected + suffix + Text[(idx + pattern.Length)..]);
            });
        }
        catch
        {
            // Silently drop
        }
    }

    // --- Spell check (debounced) ---
    private void ScheduleSpellCheck(string text)
    {
        _spellCheckCts?.Cancel();
        _spellCheckCts = new CancellationTokenSource();
        var token = _spellCheckCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(150, token);

            var match = Regex.Match(text, @"[a-zA-Z]+$");
            var word = match.Success ? match.Value : "";

            if (word.Length < 2)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    CurrentWord = "";
                    Suggestions.Clear();
                });
                return;
            }

            var results = _spellCheck.Lookup(word, ShorterOnly);

            Dispatcher.UIThread.Post(() =>
            {
                CurrentWord = word;
                Suggestions.Clear();

                if (results.Count > 0 && results[0].Distance == 0)
                    return; // Word is correct

                foreach (var s in results.Take(5))
                    Suggestions.Add(s);
            });
        }, token);
    }

    // --- Word prediction (debounced) ---
    private void SchedulePrediction(string text)
    {
        _predictionCts?.Cancel();

        if (!ApiEnabled || !text.EndsWith(' '))
        {
            Dispatcher.UIThread.Post(() => Predictions.Clear());
            return;
        }

        _predictionCts = new CancellationTokenSource();
        var token = _predictionCts.Token;
        var context = text.Trim();
        if (context.Length == 0) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(300, token);
            var results = await _smartConnection.PredictWordsAsync(context);

            Dispatcher.UIThread.Post(() =>
            {
                Predictions.Clear();
                foreach (var w in results)
                    Predictions.Add(w);
            });
        }, token);
    }

    // --- Sentence correction ---
    private string _prevText = "";

    private void CheckSentenceCorrection(string text)
    {
        if (!ApiEnabled || text.Length <= _prevText.Length || !Regex.IsMatch(text, @"[.!?,;\n]$"))
        {
            _prevText = text;
            return;
        }

        _prevText = text;

        if (_corrections.Count == 0)
        {
            // No corrections to review, but still auto-speak if enabled
            if (AutoSpeak && Regex.IsMatch(text, @"[.!?]$"))
                SpeakLastSentence(text);
            return;
        }

        Correcting = true;
        var corrections = _corrections.ToList();

        // Extract last sentence
        var textBeforePunc = text[..^1];
        var prevEnd = Math.Max(
            Math.Max(textBeforePunc.LastIndexOf('.'), textBeforePunc.LastIndexOf('!')),
            Math.Max(textBeforePunc.LastIndexOf('?'),
                Math.Max(textBeforePunc.LastIndexOf(','),
                    Math.Max(textBeforePunc.LastIndexOf(';'), textBeforePunc.LastIndexOf('\n'))))
        );
        var sentenceStart = prevEnd == -1 ? 0 : prevEnd + 1;
        var sentence = text[sentenceStart..].Trim();

        if (sentence.Length < 3)
        {
            Correcting = false;
            _corrections.Clear();
            return;
        }

        var correctionDtos = corrections
            .Select(c => new WordCorrection(c.Original, c.Corrected))
            .ToList();

        _ = Task.Run(async () =>
        {
            try
            {
                var corrected = await _smartConnection.CorrectSentenceAsync(sentence, correctionDtos);

                Dispatcher.UIThread.Post(() =>
                {
                    if (corrected != null && corrected != sentence)
                    {
                        var idx = Text.LastIndexOf(sentence, StringComparison.Ordinal);
                        if (idx != -1)
                        {
                            SetText(Text[..idx] + corrected + Text[(idx + sentence.Length)..]);
                        }
                    }

                    if (AutoSpeak)
                        SpeakLastSentence(Text);

                    Correcting = false;
                    _corrections.Clear();
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Correcting = false;
                    _corrections.Clear();
                });
            }
        });
    }

    private void SpeakLastSentence(string text)
    {
        var match = Regex.Match(text, @"[^.!?]*[.!?]\s*$");
        if (!match.Success) return;
        var sentence = match.Value.Trim();
        if (sentence.Length > 0) _tts.Speak(sentence);
    }

    // --- Key state updates ---
    private void ClearStickyMods()
    {
        _stickyMods.Clear();
        UpdateKeyStates();
        UpdateKeyLabels();
    }

    private void UpdateKeyStates()
    {
        foreach (var row in Rows)
        {
            foreach (var key in row)
            {
                key.IsPressed = _pressedKeys.Contains(key.Code);
                key.IsToggled = _stickyMods.Contains(key.Code)
                    || (key.Code == "CapsLock" && CapsLock);
            }
        }
    }

    private void UpdateKeyLabels()
    {
        var shiftActive = _stickyMods.Contains("ShiftLeft") || _stickyMods.Contains("ShiftRight")
            || _pressedKeys.Contains("ShiftLeft") || _pressedKeys.Contains("ShiftRight");

        foreach (var row in Rows)
        {
            foreach (var key in row)
            {
                if (key.Code.StartsWith("Key") && !key.Special)
                {
                    var upper = shiftActive != CapsLock;
                    key.DisplayLabel = upper
                        ? key.Definition.Label.ToUpperInvariant()
                        : key.Definition.Label.ToLowerInvariant();
                }
            }
        }
    }

    // --- Key-to-char mapping ---
    private static char? KeyToChar(string code, bool shift, bool capsLock)
    {
        if (code.StartsWith("Key") && code.Length == 4)
        {
            var letter = code[3];
            var upper = shift != capsLock;
            return upper ? char.ToUpper(letter) : char.ToLower(letter);
        }

        return code switch
        {
            "Space" => ' ',
            "Comma" => ',',
            "Period" => '.',
            "Digit1" => shift ? '!' : '1',
            "Slash" => shift ? '?' : '/',
            _ => null
        };
    }

    // --- Public method to append text (for quick phrases) ---
    public void AppendText(string phraseText)
    {
        var needsSpace = Text.Length > 0 && !Text.EndsWith(' ') && !Text.EndsWith('\n');
        SetText(Text + (needsSpace ? " " : "") + phraseText);
    }
}
