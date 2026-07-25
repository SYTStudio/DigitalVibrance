using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace DigitalVibrance.Localization;

/// <summary>
/// XAML shorthand for a localized string: <c>Text="{loc:T GamesTitle}"</c>.
///
/// It returns a binding rather than a plain string, so every translated label updates the
/// moment the language changes instead of waiting for the window to be recreated.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }

    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
