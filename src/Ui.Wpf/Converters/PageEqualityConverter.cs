using System.Globalization;
using System.Windows.Data;
using Ui.Wpf.ViewModels;

namespace Ui.Wpf.Converters;

/// <summary>
/// Compara el valor (AppPage) con el parámetro (AppPage) y devuelve bool.
/// Usado para marcar el nav item activo.
/// </summary>
[ValueConversion(typeof(AppPage), typeof(bool))]
public sealed class PageEqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppPage page && parameter is AppPage target && page == target;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
