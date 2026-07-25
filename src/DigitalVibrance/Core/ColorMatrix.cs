using System;

namespace DigitalVibrance.Core;

/// <summary>
/// A 5x5 colour transform in the layout Magnification.dll expects: row-major, applied to the
/// row vector [r g b a 1]. Row 5 is therefore the constant/offset row.
///
/// Because transforms compose by multiplication, "apply A then B" is <c>A * B</c>.
/// </summary>
public readonly struct ColorMatrix : IEquatable<ColorMatrix>
{
    private readonly float[] _m;

    private ColorMatrix(float[] m) => _m = m;

    /// <summary>The 25 coefficients, row-major. Never mutate the result.</summary>
    public float[] Values => _m ?? Identity._m;

    public static ColorMatrix Identity { get; } = new(new[]
    {
        1f, 0f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f, 0f,
        0f, 0f, 1f, 0f, 0f,
        0f, 0f, 0f, 1f, 0f,
        0f, 0f, 0f, 0f, 1f,
    });

    /// <summary>
    /// Saturation around Rec.709 luma. 0 = greyscale, 1 = untouched, &gt;1 = more vivid.
    /// This is the same shape of transform NVIDIA's Digital Vibrance applies in the driver.
    /// </summary>
    public static ColorMatrix Saturation(float s)
    {
        const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        float ir = (1f - s) * lr, ig = (1f - s) * lg, ib = (1f - s) * lb;
        return new(new[]
        {
            ir + s, ir,     ir,     0f, 0f,
            ig,     ig + s, ig,     0f, 0f,
            ib,     ib,     ib + s, 0f, 0f,
            0f,     0f,     0f,     1f, 0f,
            0f,     0f,     0f,     0f, 1f,
        });
    }

    /// <summary>Contrast pivoting around mid grey. 1 = untouched.</summary>
    public static ColorMatrix Contrast(float c)
    {
        float o = 0.5f * (1f - c);
        return new(new[]
        {
            c,  0f, 0f, 0f, 0f,
            0f, c,  0f, 0f, 0f,
            0f, 0f, c,  0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            o,  o,  o,  0f, 1f,
        });
    }

    /// <summary>Additive brightness in the -0.5..+0.5 range. 0 = untouched.</summary>
    public static ColorMatrix Brightness(float b)
    {
        return new(new[]
        {
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            b,  b,  b,  0f, 1f,
        });
    }

    /// <summary>
    /// Per-channel gain. <paramref name="temperature"/> -1..+1 goes cool..warm,
    /// <paramref name="tint"/> -1..+1 goes green..magenta.
    /// </summary>
    public static ColorMatrix WhiteBalance(float temperature, float tint)
    {
        float r = 1f + temperature * 0.22f + tint * 0.08f;
        float g = 1f - tint * 0.16f;
        float b = 1f - temperature * 0.22f + tint * 0.08f;
        return new(new[]
        {
            r,  0f, 0f, 0f, 0f,
            0f, g,  0f, 0f, 0f,
            0f, 0f, b,  0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            0f, 0f, 0f, 0f, 1f,
        });
    }

    public static ColorMatrix operator *(ColorMatrix a, ColorMatrix b)
    {
        float[] x = a.Values, y = b.Values;
        var r = new float[25];
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                float sum = 0f;
                for (int k = 0; k < 5; k++)
                    sum += x[i * 5 + k] * y[k * 5 + j];
                r[i * 5 + j] = sum;
            }
        }
        return new(r);
    }

    public static ColorMatrix Lerp(ColorMatrix a, ColorMatrix b, float t)
    {
        float[] x = a.Values, y = b.Values;
        var r = new float[25];
        for (int i = 0; i < 25; i++)
            r[i] = x[i] + (y[i] - x[i]) * t;
        return new(r);
    }

    public bool Equals(ColorMatrix other)
    {
        float[] x = Values, y = other.Values;
        for (int i = 0; i < 25; i++)
            if (MathF.Abs(x[i] - y[i]) > 0.0005f)
                return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is ColorMatrix m && Equals(m);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (float v in Values) h.Add(MathF.Round(v, 3));
        return h.ToHashCode();
    }
}
