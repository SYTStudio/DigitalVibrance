using System.Collections.Generic;
using System.Text.Json.Serialization;
using DigitalVibrance.Core;

namespace DigitalVibrance.Localization;

/// <summary>One language, loaded from a JSON file in Localization/Languages.</summary>
public sealed class LanguagePack : ObservableObject
{
    private bool _isCurrent;

    /// <summary>ISO code used for matching and persistence, e.g. "de".</summary>
    public string Code { get; set; } = "en";

    /// <summary>Name written in the language itself — that is what the picker shows.</summary>
    public string Name { get; set; } = "";

    /// <summary>Shown underneath so the list stays navigable in an unfamiliar script.</summary>
    public string EnglishName { get; set; } = "";

    /// <summary>Right-to-left script; mirrors the whole window layout.</summary>
    public bool Rtl { get; set; }

    public Dictionary<string, string> Strings { get; set; } = new();

    [JsonIgnore]
    public bool IsCurrent
    {
        get => _isCurrent;
        set => Set(ref _isCurrent, value);
    }
}
