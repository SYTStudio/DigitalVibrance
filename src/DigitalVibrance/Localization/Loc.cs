using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows.Data;

namespace DigitalVibrance.Localization;

/// <summary>
/// The string table. A singleton with a string indexer so XAML can bind to
/// <c>[SomeKey]</c>; raising <see cref="Binding.IndexerName"/> makes every bound string in the
/// window refresh at once, which is what allows switching language without a restart.
///
/// Any key missing from the active pack falls back to English rather than showing blank text.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private const string FallbackCode = "en";

    private readonly List<LanguagePack> _packs;
    private readonly LanguagePack _fallback;
    private LanguagePack _current;

    public static Loc Instance { get; } = new();

    private Loc()
    {
        _packs = LoadPacks();
        _fallback = _packs.FirstOrDefault(p => p.Code == FallbackCode) ?? new LanguagePack();
        _current = _fallback;
        _current.IsCurrent = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the active language changes, for things that are not data-bound.</summary>
    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguagePack> Available => _packs;

    public LanguagePack Current => _current;

    public bool IsRightToLeft => _current.Rtl;

    /// <summary>Keeps message boxes readable in right-to-left languages.</summary>
    public System.Windows.MessageBoxOptions DialogOptions => _current.Rtl
        ? System.Windows.MessageBoxOptions.RtlReading | System.Windows.MessageBoxOptions.RightAlign
        : System.Windows.MessageBoxOptions.None;

    public string this[string key]
    {
        get
        {
            if (_current.Strings.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
                return value;
            if (_fallback.Strings.TryGetValue(key, out string? english) && !string.IsNullOrEmpty(english))
                return english;
            return key; // visible in the UI, which makes a missing key obvious rather than silent
        }
    }

    public string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(this[key], args);
        }
        catch (FormatException)
        {
            return this[key];
        }
    }

    /// <summary>Switches language. Unknown or null codes fall back to English.</summary>
    public void Use(string? code)
    {
        var pack = _packs.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))
                   ?? _fallback;

        if (ReferenceEquals(pack, _current)) return;

        _current.IsCurrent = false;
        _current = pack;
        _current.IsCurrent = true;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Best match for the OS display language, used on first run before the user has chosen.
    /// Tries the full culture first so regional variants can be added later without code changes.
    /// </summary>
    public string DetectSystemCode()
    {
        var culture = CultureInfo.CurrentUICulture;

        foreach (string candidate in new[] { culture.Name, culture.TwoLetterISOLanguageName })
        {
            if (string.IsNullOrEmpty(candidate)) continue;
            var hit = _packs.FirstOrDefault(p => string.Equals(p.Code, candidate, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit.Code;
        }

        return FallbackCode;
    }

    // ---------- loading ----------

    private static List<LanguagePack> LoadPacks()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var byCode = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);

        // Built-in packs ship embedded so they can never go missing.
        var assembly = typeof(Loc).Assembly;
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(".Languages.", StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            var pack = TryRead(() => assembly.GetManifestResourceStream(name), options);
            if (pack is not null) byCode[pack.Code] = pack;
        }

        // Anything dropped into %AppData%\DigitalVibrance\Languages wins, so a translation can be
        // fixed or a new language added without rebuilding.
        foreach (var pack in LoadUserPacks(options))
            byCode[pack.Code] = pack;

        return byCode.Values
            .OrderBy(p => p.Code == FallbackCode ? 0 : 1)
            .ThenBy(p => p.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<LanguagePack> LoadUserPacks(JsonSerializerOptions options)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalVibrance", "Languages");

        string[] files;
        try
        {
            if (!Directory.Exists(directory)) yield break;
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string file in files)
        {
            var pack = TryRead(() => File.OpenRead(file), options);
            if (pack is not null) yield return pack;
        }
    }

    private static LanguagePack? TryRead(Func<Stream?> open, JsonSerializerOptions options)
    {
        try
        {
            using Stream? stream = open();
            if (stream is null) return null;

            var pack = JsonSerializer.Deserialize<LanguagePack>(stream, options);
            return string.IsNullOrWhiteSpace(pack?.Code) ? null : pack;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null; // a broken pack is skipped; the rest of the app carries on
        }
    }
}
