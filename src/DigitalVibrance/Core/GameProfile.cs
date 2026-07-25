using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DigitalVibrance.Core;

public enum ActivationMode
{
    /// <summary>Colours apply only while the game window has focus. Alt-tab restores the desktop.</summary>
    Foreground = 0,
    /// <summary>Colours apply the whole time the process exists, focused or not.</summary>
    WhileRunning = 1,
}

public sealed class GameProfile : ObservableObject
{
    private string _name = "";
    private string _executablePath = "";
    private bool _enabled = true;
    private ActivationMode _mode = ActivationMode.Foreground;
    private bool _isSelected;
    private bool _isActive;
    private ImageSource? _icon;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            if (!Set(ref _executablePath, value)) return;
            Raise(nameof(ExecutableName));
            Raise(nameof(MatchKey));
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public ActivationMode Mode
    {
        get => _mode;
        set
        {
            if (!Set(ref _mode, value)) return;
            Raise(nameof(IsForegroundMode));
            Raise(nameof(IsWhileRunningMode));
        }
    }

    public ColorProfile Color { get; set; } = new();

    /// <summary>e.g. "cs2.exe" — what actually gets matched against running processes.</summary>
    [JsonIgnore]
    public string ExecutableName =>
        string.IsNullOrEmpty(_executablePath) ? "" : Path.GetFileName(_executablePath);

    /// <summary>Lower-cased executable name. Matching by name survives the game moving folders.</summary>
    [JsonIgnore]
    public string MatchKey => ExecutableName.ToLowerInvariant();

    // The two segmented buttons bind straight to these, which avoids an enum converter.

    [JsonIgnore]
    public bool IsForegroundMode
    {
        get => _mode == ActivationMode.Foreground;
        set { if (value) Mode = ActivationMode.Foreground; }
    }

    [JsonIgnore]
    public bool IsWhileRunningMode
    {
        get => _mode == ActivationMode.WhileRunning;
        set { if (value) Mode = ActivationMode.WhileRunning; }
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>True while this profile is the one currently painted on screen.</summary>
    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }
}
