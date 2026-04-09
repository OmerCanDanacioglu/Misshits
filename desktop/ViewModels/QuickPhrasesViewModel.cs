using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Misshits.Desktop.Models;
using Misshits.Desktop.Services;

namespace Misshits.Desktop.ViewModels;

public partial class QuickPhrasesViewModel : ViewModelBase
{
    private readonly IQuickPhraseService _service;
    private readonly ITextBuffer _textBuffer;

    public ObservableCollection<QuickPhrase> Phrases { get; } = new();

    public QuickPhrasesViewModel(IQuickPhraseService service, ITextBuffer textBuffer)
    {
        _service = service;
        _textBuffer = textBuffer;
        _ = LoadPhrasesAsync();
    }

    private async Task LoadPhrasesAsync()
    {
        var phrases = await _service.GetAllAsync();
        Dispatcher.UIThread.Post(() =>
        {
            Phrases.Clear();
            foreach (var p in phrases) Phrases.Add(p);
        });
    }

    [RelayCommand]
    private async Task UsePhrase(int id)
    {
        var text = await _service.UseAsync(id);
        if (text != null)
            Dispatcher.UIThread.Post(() => _textBuffer.AppendText(text, smartSpacing: true));
        await LoadPhrasesAsync();
    }

    [RelayCommand]
    private async Task SaveCurrentText()
    {
        var text = _textBuffer.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        await _service.AddAsync(text);
        await LoadPhrasesAsync();
    }

    [RelayCommand]
    private async Task DeletePhrase(int id)
    {
        await _service.DeleteAsync(id);
        await LoadPhrasesAsync();
    }
}
