using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public interface IThemeService
{
    void Apply(AppTheme theme);
}
