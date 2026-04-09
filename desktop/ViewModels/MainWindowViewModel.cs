namespace Misshits.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public KeyboardViewModel Keyboard { get; }
    public QuickPhrasesViewModel QuickPhrases { get; }

    public MainWindowViewModel(KeyboardViewModel keyboard, QuickPhrasesViewModel quickPhrases)
    {
        Keyboard = keyboard;
        QuickPhrases = quickPhrases;
    }
}
