using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalVibrance.Views;

/// <summary>Label + value readout + styled slider. Used for every colour control.</summary>
public partial class SliderRow : UserControl
{
    public SliderRow()
    {
        InitializeComponent();
        UpdateValueBrush(); // the default value never fires the changed callback
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderRow),
            new PropertyMetadata(""));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.Register(nameof(Hint), typeof(string), typeof(SliderRow),
            new PropertyMetadata("", OnHintChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SliderRow),
            new PropertyMetadata("%"));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderRow),
            new FrameworkPropertyMetadata(50d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow),
            new PropertyMetadata(100d));

    public static readonly DependencyProperty NeutralProperty =
        DependencyProperty.Register(nameof(Neutral), typeof(double), typeof(SliderRow),
            new PropertyMetadata(50d, OnValueChanged));

    private static readonly DependencyPropertyKey ValueBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(ValueBrush), typeof(Brush), typeof(SliderRow),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ValueBrushProperty = ValueBrushKey.DependencyProperty;

    private static readonly DependencyPropertyKey HasHintKey =
        DependencyProperty.RegisterReadOnly(nameof(HasHint), typeof(bool), typeof(SliderRow),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasHintProperty = HasHintKey.DependencyProperty;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The value considered "no change"; the readout dims when the slider sits on it.</summary>
    public double Neutral
    {
        get => (double)GetValue(NeutralProperty);
        set => SetValue(NeutralProperty, value);
    }

    /// <summary>Dim while neutral, bright once the setting actually does something.</summary>
    public Brush? ValueBrush => (Brush?)GetValue(ValueBrushProperty);

    public bool HasHint => (bool)GetValue(HasHintProperty);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SliderRow)d).UpdateValueBrush();

    private static void OnHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => d.SetValue(HasHintKey, !string.IsNullOrWhiteSpace(e.NewValue as string));

    private void UpdateValueBrush()
    {
        object key = System.Math.Abs(Value - Neutral) < 0.5 ? "TextDim" : "Accent2";
        SetValue(ValueBrushKey, TryFindResource(key) as Brush);
    }
}
