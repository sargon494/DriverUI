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
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppPage page)
        {
            if (parameter is AppPage target)
            {
                return page == target;
            }

            if (parameter is string str && Enum.TryParse<AppPage>(str, out var parsed))
            {
                return page == parsed;
            }
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
