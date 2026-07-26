using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace DigitalVibrance.Services;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// Swaps the palette dictionary merged at index 0 of <see cref="Application.Resources"/>.
///
/// Because every colour in Theme.xaml and the views is referenced through DynamicResource,
/// replacing that one dictionary repaints the open window — no restart, no rebuilt views.
/// </summary>
public sealed class ThemeManager : INotifyPropertyChanged
{
    public static ThemeManager Instance { get; } = new();

    private AppTheme _current = AppTheme.Dark;

    private ThemeManager() { }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ThemeChanged;

    public AppTheme Current => _current;

    public bool IsDark => _current == AppTheme.Dark;

    /// <summary>
    /// Segoe MDL2 glyph for the button, labelling the theme it switches *to*:
    /// a sun while dark, a moon while light.
    /// </summary>
    public string ToggleGlyph => _current == AppTheme.Dark ? "\uE706" : "\uE708";

    public void Use(AppTheme theme)
    {
        if (_current == theme && Application.Current is not null) return;

        var app = Application.Current;
        if (app is null) return;

        var dictionaries = app.Resources.MergedDictionaries;
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative),
        };

        if (dictionaries.Count == 0) dictionaries.Add(replacement);
        else dictionaries[0] = replacement;

        _current = theme;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDark)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleGlyph)));
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => Use(_current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    /// <summary>
    /// Reads the Windows app theme so a first run matches the rest of the desktop. Falls back to
    /// dark, which is what the palette was designed around.
    /// </summary>
    public static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // 0 = dark, 1 = light
            if (key?.GetValue("AppsUseLightTheme") is int value) return value == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            // registry unavailable or locked down - fall through
        }

        return AppTheme.Dark;
    }

    public static AppTheme Parse(string? name) =>
        Enum.TryParse<AppTheme>(name, ignoreCase: true, out var theme) ? theme : DetectSystemTheme();
}
