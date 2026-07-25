using System;
using System.Text.Json.Serialization;

namespace DigitalVibrance.Core;

/// <summary>
/// One set of colour values. Every slider uses 0..100 with <b>50 = neutral</b>, matching the
/// scale NVIDIA Control Panel uses for Digital Vibrance so the numbers feel familiar.
/// Vibrance alone can go past 100 once <see cref="OverrideEnabled"/> is on.
/// </summary>
public sealed class ColorProfile : ObservableObject
{
    public const int Neutral = 50;
    public const int NormalMax = 100;
    public const int OverrideMax = 200;

    private int _vibrance = Neutral;
    private bool _overrideEnabled;
    private int _contrast = Neutral;
    private int _brightness = Neutral;
    private int _temperature = Neutral;
    private int _tint = Neutral;

    /// <summary>0 = greyscale, 50 = untouched, 100 = fully vivid, up to 200 with override.</summary>
    /// <remarks>
    /// Clamped to the absolute ceiling rather than to <see cref="VibranceMax"/>: during JSON
    /// deserialization this can be assigned before <see cref="OverrideEnabled"/> has been read,
    /// and clamping against the not-yet-restored flag would silently cap saved profiles at 100.
    /// <see cref="Normalize"/> re-imposes the override rule once the whole object is loaded.
    /// </remarks>
    public int Vibrance
    {
        get => _vibrance;
        set
        {
            if (Set(ref _vibrance, Math.Clamp(value, 0, OverrideMax)))
                Raise(nameof(IsNeutral));
        }
    }

    /// <summary>Unlocks the extra +100% above the normal driver-equivalent range.</summary>
    public bool OverrideEnabled
    {
        get => _overrideEnabled;
        set
        {
            if (!Set(ref _overrideEnabled, value)) return;
            Raise(nameof(VibranceMax));
            if (!value && _vibrance > NormalMax) Vibrance = NormalMax;
            Raise(nameof(IsNeutral));
        }
    }

    [JsonIgnore]
    public int VibranceMax => _overrideEnabled ? OverrideMax : NormalMax;

    public int Contrast
    {
        get => _contrast;
        set { if (Set(ref _contrast, Math.Clamp(value, 0, 100))) Raise(nameof(IsNeutral)); }
    }

    public int Brightness
    {
        get => _brightness;
        set { if (Set(ref _brightness, Math.Clamp(value, 0, 100))) Raise(nameof(IsNeutral)); }
    }

    /// <summary>0 = cool/blue, 50 = neutral, 100 = warm/orange.</summary>
    public int Temperature
    {
        get => _temperature;
        set { if (Set(ref _temperature, Math.Clamp(value, 0, 100))) Raise(nameof(IsNeutral)); }
    }

    /// <summary>0 = green, 50 = neutral, 100 = magenta.</summary>
    public int Tint
    {
        get => _tint;
        set { if (Set(ref _tint, Math.Clamp(value, 0, 100))) Raise(nameof(IsNeutral)); }
    }

    [JsonIgnore]
    public bool IsNeutral =>
        _vibrance == Neutral && _contrast == Neutral && _brightness == Neutral &&
        _temperature == Neutral && _tint == Neutral;

    /// <summary>
    /// Composes the sliders into one transform. Order matters: white balance first so
    /// saturation works on the corrected hue, then contrast, then the brightness offset.
    /// </summary>
    public ColorMatrix Build()
    {
        if (IsNeutral) return ColorMatrix.Identity;

        var wb = ColorMatrix.WhiteBalance((_temperature - 50) / 50f, (_tint - 50) / 50f);
        var sat = ColorMatrix.Saturation(_vibrance / 50f);
        var con = ColorMatrix.Contrast(_contrast / 50f);
        var bri = ColorMatrix.Brightness((_brightness - 50) / 100f);
        return wb * sat * con * bri;
    }

    /// <summary>
    /// Restores the invariants after loading, where property order is not guaranteed and the
    /// file may have been hand-edited. Writes fields directly — nothing is bound to it yet.
    /// </summary>
    public void Normalize()
    {
        _vibrance = Math.Clamp(_vibrance, 0, _overrideEnabled ? OverrideMax : NormalMax);
        _contrast = Math.Clamp(_contrast, 0, 100);
        _brightness = Math.Clamp(_brightness, 0, 100);
        _temperature = Math.Clamp(_temperature, 0, 100);
        _tint = Math.Clamp(_tint, 0, 100);
    }

    public void ResetToNeutral()
    {
        Vibrance = Neutral;
        OverrideEnabled = false;
        Contrast = Neutral;
        Brightness = Neutral;
        Temperature = Neutral;
        Tint = Neutral;
    }

    public ColorProfile Clone() => new()
    {
        _overrideEnabled = _overrideEnabled,
        _vibrance = _vibrance,
        _contrast = _contrast,
        _brightness = _brightness,
        _temperature = _temperature,
        _tint = _tint,
    };

    public void CopyFrom(ColorProfile other)
    {
        OverrideEnabled = other.OverrideEnabled;
        Vibrance = other.Vibrance;
        Contrast = other.Contrast;
        Brightness = other.Brightness;
        Temperature = other.Temperature;
        Tint = other.Tint;
    }
}
