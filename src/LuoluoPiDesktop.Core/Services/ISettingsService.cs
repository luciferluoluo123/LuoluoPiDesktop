using LuoluoPiDesktop.Core.Models;

namespace LuoluoPiDesktop.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void Reload();
}
