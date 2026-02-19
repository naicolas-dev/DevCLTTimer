using System.Windows;

namespace DevCLT.WindowsApp.Services;

public interface IThemeService
{
    bool IsDarkTheme { get; }
    void ToggleTheme();
}

public class ThemeService : IThemeService
{
    private const string LightSource = "Themes/ColorsLight.xaml";
    private const string DarkSource = "Themes/ColorsDark.xaml";

    public bool IsDarkTheme { get; private set; }

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var targetSource = IsDarkTheme ? DarkSource : LightSource;
        var dicts = Application.Current.Resources.MergedDictionaries;

        // Find and remove the current color dictionary
        ResourceDictionary? current = null;
        foreach (var d in dicts)
        {
            if (d.Source != null &&
                (d.Source.OriginalString.Contains("ColorsLight") || d.Source.OriginalString.Contains("ColorsDark")))
            {
                current = d;
                break;
            }
        }

        if (current != null)
            dicts.Remove(current);

        // Insert the new color dictionary at position 0 (before all other dictionaries)
        dicts.Insert(0, new ResourceDictionary { Source = new Uri(targetSource, UriKind.Relative) });
    }
}
