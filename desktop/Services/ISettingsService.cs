using Misshits.Desktop.Models;

namespace Misshits.Desktop.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
