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
    private readonly ITextBuffer _textBuffer;
    private readonly ISpellCheckService _spellCheck;
    private readonly ISmartConnectionService _smartConnection;
    private readonly ITextToSpeechService _tts;
    private readonly IAutoCorrectionService _autoCorrection;
    private readonly ISettingsService _settings;
    private readonly IContextualPhrasesService _contextualPhrases;

    // --- Keyboard layout ---
    public List<List<KeyViewModel>> Rows { get; }

    // --- Observable state ---
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _displayText = "|";
    [ObservableProperty] private bool _capsLock;
    [ObservableProperty] private bool _autoCorrectEnabled = true;
    [ObservableProperty] private bool _shorterOnly;
    [ObservableProperty] private bool _autoSpeak;
    [ObservableProperty] private bool _apiEnabled = true;
    [ObservableProperty] private bool _timeAwareSuggestions = true;
    [ObservableProperty] private bool _phrasesOpen;
    [ObservableProperty] private bool _correcting;
    [ObservableProperty] private string _currentWord = "";
    [ObservableProperty] private bool _cursorVisible = true;

    public ObservableCollection<Suggestion> Suggestions { get; } = new();
    public ObservableCollection<string> Predictions { get; } = new();
    public ObservableCollection<string> ContextualPhrases { get; } = new();
    [ObservableProperty] private string _timePeriod = "";
    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasPredictions => Predictions.Count > 0 && Suggestions.Count == 0;

    private readonly HashSet<string> _pressedKeys = new();
    private readonly HashSet<string> _stickyMods = new();
    private CancellationTokenSource? _spellCheckCts;
    private CancellationTokenSource? _predictionCts;
    private string _prevText = "";

    public KeyboardViewModel(
        ITextBuffer textBuffer,
        ISpellCheckService spellCheck,
        ISmartConnectionService smartConnection,
        ITextToSpeechService tts,
        IAutoCorrectionService autoCorrection,
        ISettingsService settings,
        IContextualPhrasesService contextualPhrases)
    {
        _textBuffer = textBuffer;
        _spellCheck = spellCheck;
        _smartConnection = smartConnection;
        _tts = tts;
        _autoCorrection = autoCorrection;
        _settings = settings;
        _contextualPhrases = contextualPhrases;

        // Load persisted settings
        var saved = settings.Load();
        _autoCorrectEnabled = saved.AutoCorrectEnabled;
        _shorterOnly = saved.ShorterOnly;
        _autoSpeak = saved.AutoSpeak;
        _apiEnabled = saved.ApiEnabled;
        _timeAwareSuggestions = saved.TimeAwareSuggestions;

        Rows = KeyboardLayouts.Qwerty.Select(row =>
            row.Select(def => new KeyViewModel(def)).ToList()
        ).ToList();

        // Sync ViewModel properties when buffer changes
        _textBuffer.TextChanged += OnBufferTextChanged;

        // Cursor blink timer
        var cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        cursorTimer.Tick += (_, _) =>
        {
            CursorVisible = !CursorVisible;
            _textBuffer.SetCursorVisible(CursorVisible);
            DisplayText = _textBuffer.DisplayText;
        };
        cursorTimer.Start();

        // Contextual phrases — refresh every minute
        RefreshContextualPhrases();
        var contextTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        contextTimer.Tick += (_, _) => RefreshContextualPhrases();
        contextTimer.Start();

        Suggestions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSuggestions));
            OnPropertyChanged(nameof(HasPredictions));
        };
        Predictions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPredictions));
    }

    private void SaveSettings()
    {
        _settings.Save(new AppSettings
        {
            AutoCorrectEnabled = AutoCorrectEnabled,
            ShorterOnly = ShorterOnly,
            AutoSpeak = AutoSpeak,
            ApiEnabled = ApiEnabled,
            TimeAwareSuggestions = TimeAwareSuggestions
        });
    }

    partial void OnAutoCorrectEnabledChanged(bool value) => SaveSettings();
    partial void OnShorterOnlyChanged(bool value) => SaveSettings();
    partial void OnAutoSpeakChanged(bool value) => SaveSettings();
    partial void OnApiEnabledChanged(bool value) => SaveSettings();
    partial void OnTimeAwareSuggestionsChanged(bool value) { SaveSettings(); RefreshContextualPhrases(); }

    private int _textChangeDepth;

    private void OnBufferTextChanged(string newText)
    {
        Text = newText;
        DisplayText = _textBuffer.DisplayText;

        if (_textChangeDepth > 0) { _prevText = newText; return; }
        _textChangeDepth++;
        try
        {
            ScheduleSpellCheck(newText);
            SchedulePrediction(newText);
            CheckSentenceCorrection(newText);
        }
        catch
        {
            // Swallow to prevent UI thread crash
        }
        finally
        {
            _textChangeDepth--;
        }
    }

    private void RefreshContextualPhrases()
    {
        ContextualPhrases.Clear();
        if (!TimeAwareSuggestions) { TimePeriod = ""; return; }
        TimePeriod = _contextualPhrases.CurrentTimePeriod;
        foreach (var p in _contextualPhrases.GetCurrentPhrases()) ContextualPhrases.Add(p);
    }

    // --- Commands ---
    [RelayCommand]
    private void InsertContextualPhrase(string phrase) =>
        _textBuffer.AppendText(phrase, smartSpacing: true);

    [RelayCommand]
    private void Speak()
    {
        if (!string.IsNullOrEmpty(Text))
            _tts.Speak(Text);
    }

    [RelayCommand]
    private void Clear() => _textBuffer.Clear();

    [RelayCommand]
    private void Undo() => _textBuffer.Undo();

    [RelayCommand]
    private void TogglePhrases() => PhrasesOpen = !PhrasesOpen;

    [RelayCommand]
    private void ApplySuggestion(string term)
    {
        var match = Regex.Match(Text, @"[a-zA-Z]+$");
        if (!match.Success) return;
        _textBuffer.SetText(Text[..match.Index] + term);
    }

    [RelayCommand]
    private void InsertPrediction(string word) =>
        _textBuffer.AppendText(word + " ", smartSpacing: true);

    // --- Physical keyboard handling ---
    public void HandlePhysicalKeyDown(string code, bool ctrlKey, bool shiftKey)
    {
        try
        {
        _pressedKeys.Add(code);
        UpdateKeyStates();

        if (code == "CapsLock") { CapsLock = !CapsLock; UpdateKeyLabels(); return; }
        if (KeyboardLayouts.ModifierCodes.Contains(code)) return;

        if (code == "Backspace")
        {
            if (ctrlKey)
            {
                var trimmed = Text.TrimEnd();
                var lastWord = Regex.Match(trimmed, @"\S+$");
                _textBuffer.SetText(lastWord.Success ? Text[..lastWord.Index] : "");
            }
            else if (Text.Length > 0)
            {
                _textBuffer.SetText(Text[..^1]);
            }
            return;
        }

        if (code == "Enter") { _textBuffer.AppendText("\n"); return; }
        if (code == "Space") { HandleAutoCorrectAppend(" "); ClearStickyMods(); return; }

        var ch = KeyToChar(code, shiftKey, CapsLock);
        if (ch != null)
        {
            if (".!?,;:'\"()-".Contains(ch.Value))
                HandleAutoCorrectAppend(ch.Value.ToString());
            else
                _textBuffer.AppendText(ch.Value.ToString());
        }
        ClearStickyMods();
        }
        catch
        {
            // Swallow to prevent UI thread crash
        }
    }

    public void HandlePhysicalKeyUp(string code)
    {
        _pressedKeys.Remove(code);
        UpdateKeyStates();
    }

    // --- Virtual keyboard click ---
    [RelayCommand]
    private void MouseClick(string code)
    {
        if (code == "CapsLock") { CapsLock = !CapsLock; UpdateKeyLabels(); return; }

        if (KeyboardLayouts.ModifierCodes.Contains(code))
        {
            if (_stickyMods.Contains(code)) _stickyMods.Remove(code);
            else _stickyMods.Add(code);
            UpdateKeyStates();
            return;
        }

        if (code == "Backspace")
        {
            if (Text.Length > 0) _textBuffer.SetText(Text[..^1]);
            ClearStickyMods(); return;
        }
        if (code == "BackspaceWord")
        {
            var trimmed = Text.TrimEnd();
            var lastWord = Regex.Match(trimmed, @"\S+$");
            _textBuffer.SetText(lastWord.Success ? Text[..lastWord.Index] : "");
            ClearStickyMods(); return;
        }
        if (code == "Enter") { _textBuffer.AppendText("\n"); ClearStickyMods(); return; }
        if (code == "Space") { HandleAutoCorrectAppend(" "); ClearStickyMods(); return; }

        var shift = _stickyMods.Contains("ShiftLeft") || _stickyMods.Contains("ShiftRight");
        var ch = KeyToChar(code, shift, CapsLock);
        if (ch != null)
        {
            if (".!?,;:'\"()-".Contains(ch.Value))
                HandleAutoCorrectAppend(ch.Value.ToString());
            else
                _textBuffer.AppendText(ch.Value.ToString());
        }
        ClearStickyMods();
    }

    // --- Auto-correct orchestration ---
    private void HandleAutoCorrectAppend(string suffix)
    {
        var currentText = Text;
        var newText = _autoCorrection.AutoCorrectAndAppend(
            currentText, suffix, CurrentWord, Suggestions, AutoCorrectEnabled);

        // Check if deferred correction is needed (suggestions were stale)
        string? deferredWord = null;
        if (newText == currentText + suffix && AutoCorrectEnabled)
        {
            var match = Regex.Match(currentText, @"[a-zA-Z]+$");
            if (match.Success && match.Value.Length >= 2)
                deferredWord = match.Value;
        }

        // Apply the text change
        _textBuffer.SetText(newText);

        // Fire deferred correction AFTER SetText (non-blocking)
        if (deferredWord != null)
        {
            var word = deferredWord;
            var sfx = suffix;
            _ = Task.Run(() =>
            {
                try
                {
                    var correction = _autoCorrection.TryDeferredCorrection(word);
                    if (correction == null) return;

                    Dispatcher.UIThread.Post(() =>
                    {
                        var pattern = word + sfx;
                        var idx = _textBuffer.Text.LastIndexOf(pattern, StringComparison.Ordinal);
                        if (idx == -1) return;
                        _textBuffer.SetText(
                            _textBuffer.Text[..idx] + correction.Corrected + sfx +
                            _textBuffer.Text[(idx + pattern.Length)..]);
                    });
                }
                catch
                {
                    // Silently drop
                }
            });
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
                Dispatcher.UIThread.Post(() => { CurrentWord = ""; Suggestions.Clear(); });
                return;
            }

            var results = _spellCheck.Lookup(word, ShorterOnly);
            Dispatcher.UIThread.Post(() =>
            {
                CurrentWord = word;
                Suggestions.Clear();
                if (results.Count > 0 && results[0].Distance == 0) return;
                foreach (var s in results.Take(5)) Suggestions.Add(s);
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
                foreach (var w in results) Predictions.Add(w);
            });
        }, token);
    }

    // --- Sentence correction ---
    private bool _correctionInFlight;

    private void CheckSentenceCorrection(string text)
    {
        if (_correctionInFlight) return;
        if (!ApiEnabled || text.Length <= _prevText.Length || !Regex.IsMatch(text, @"[.!?\n]$"))
        {
            _prevText = text;
            return;
        }
        _prevText = text;

        if (_autoCorrection.Corrections.Count == 0)
        {
            if (AutoSpeak && Regex.IsMatch(text, @"[.!?]$"))
                SpeakLastSentence(text);
            return;
        }

        _correctionInFlight = true;
        Correcting = true;
        var corrections = _autoCorrection.Corrections.ToList();

        var textBeforePunc = text[..^1];
        var prevEnd = new[] { '.', '!', '?', ',', ';', '\n' }
            .Max(c => textBeforePunc.LastIndexOf(c));
        var sentenceStart = prevEnd == -1 ? 0 : prevEnd + 1;
        var sentence = text[sentenceStart..].Trim();

        if (sentence.Length < 3)
        {
            Correcting = false;
            _correctionInFlight = false;
            _autoCorrection.ClearCorrections();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var correctionTask = _smartConnection.CorrectSentenceAsync(sentence, corrections);
                var completed = await Task.WhenAny(correctionTask, Task.Delay(-1, cts.Token));
                var corrected = completed == correctionTask ? await correctionTask : null;
                // Validate: reject if response is wildly different length (LLM echoed prompt)
                if (corrected != null && corrected.Length > sentence.Length * 3)
                    corrected = null;

                Dispatcher.UIThread.Post(() =>
                {
                    if (corrected != null && corrected != sentence)
                    {
                        var idx = _textBuffer.Text.LastIndexOf(sentence, StringComparison.Ordinal);
                        if (idx != -1)
                            _textBuffer.SetText(_textBuffer.Text[..idx] + corrected +
                                _textBuffer.Text[(idx + sentence.Length)..]);
                    }
                    if (AutoSpeak) SpeakLastSentence(_textBuffer.Text);
                    Correcting = false;
                    _correctionInFlight = false;
                    _autoCorrection.ClearCorrections();
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Correcting = false;
                    _correctionInFlight = false;
                    _autoCorrection.ClearCorrections();
                });
            }
        });
    }

    private void SpeakLastSentence(string text)
    {
        var match = Regex.Match(text, @"[^.!?]*[.!?]\s*$");
        if (match.Success && match.Value.Trim().Length > 0)
            _tts.Speak(match.Value.Trim());
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
        foreach (var key in row)
        {
            key.IsPressed = _pressedKeys.Contains(key.Code);
            key.IsToggled = _stickyMods.Contains(key.Code)
                || (key.Code == "CapsLock" && CapsLock);
        }
    }

    private void UpdateKeyLabels()
    {
        var shiftActive = _stickyMods.Contains("ShiftLeft") || _stickyMods.Contains("ShiftRight")
            || _pressedKeys.Contains("ShiftLeft") || _pressedKeys.Contains("ShiftRight");
        foreach (var row in Rows)
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

    // --- Key-to-char mapping ---
    private static char? KeyToChar(string code, bool shift, bool capsLock)
    {
        if (code.StartsWith("Key") && code.Length == 4)
        {
            var letter = code[3];
            return (shift != capsLock) ? char.ToUpper(letter) : char.ToLower(letter);
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

    // --- Public for quick phrases ---
    public void AppendText(string phraseText) =>
        _textBuffer.AppendText(phraseText, smartSpacing: true);
}
