using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Agent.Ipc.Grpc;

namespace Ui.Wpf.Converters;

// ────────────────────────────────────────────────────────────────
// Bool → Visibility
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert)
        {
            b = !b;
        }

        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

// ────────────────────────────────────────────────────────────────
// Null → Visibility
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        bool visible = Invert ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// DriverStatusProto → Color brush
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(DriverStatusProto), typeof(Brush))]
public sealed class DriverStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DriverStatusProto status)
        {
            return Brushes.Transparent;
        }

        return status switch
        {
            DriverStatusProto.DriverStatusOk => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
            DriverStatusProto.DriverStatusError => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            DriverStatusProto.DriverStatusDegraded => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            _ => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// DriverStatusProto → Text label
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(DriverStatusProto), typeof(string))]
public sealed class DriverStatusToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DriverStatusProto s ? s switch
        {
            DriverStatusProto.DriverStatusOk => "OK",
            DriverStatusProto.DriverStatusError => "Error",
            DriverStatusProto.DriverStatusDegraded => "Degradado",
            _ => "Desconocido",
        } : "?";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// InstallPhaseProto → Text label
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(InstallPhaseProto), typeof(string))]
public sealed class InstallPhaseToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallPhaseProto p ? p switch
        {
            InstallPhaseProto.InstallPhaseDownloading => "Descargando...",
            InstallPhaseProto.InstallPhaseVerifying => "Verificando firma...",
            InstallPhaseProto.InstallPhaseCreatingRestorePoint => "Creando punto de restauración...",
            InstallPhaseProto.InstallPhaseInstalling => "Instalando...",
            InstallPhaseProto.InstallPhaseValidating => "Validando...",
            InstallPhaseProto.InstallPhaseComplete => "Completado",
            InstallPhaseProto.InstallPhaseFailed => "Error",
            InstallPhaseProto.InstallPhaseRollingBack => "Deshaciendo...",
            _ => "Esperando...",
        } : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// Bytes → human-readable size
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(long), typeof(string))]
public sealed class BytesToHumanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "—";
        }

        return bytes switch
        {
            < 0 => "—",
            0 => "—",
            < 1024 => $"{bytes} B",
            < 1_048_576 => $"{bytes / 1024.0:F1} KB",
            < 1_073_741_824 => $"{bytes / 1_048_576.0:F1} MB",
            _ => $"{bytes / 1_073_741_824.0:F2} GB",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// Timestamp (Google.Protobuf) → local datetime string
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(Google.Protobuf.WellKnownTypes.Timestamp), typeof(string))]
public sealed class TimestampToStringConverter : IValueConverter
{
    public string Format { get; set; } = "dd MMM yyyy  HH:mm";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Google.Protobuf.WellKnownTypes.Timestamp ts)
        {
            return "—";
        }

        var dt = ts.ToDateTimeOffset().LocalDateTime;
        return dt == default ? "—" : dt.ToString(Format, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// int (count) → Visibility (visible if > 0)
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// bool Success → brush (success/error colors)
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class SuccessToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
            : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

// ────────────────────────────────────────────────────────────────
// int → string (badge: "0" si nada, número si hay)
// ────────────────────────────────────────────────────────────────

[ValueConversion(typeof(int), typeof(string))]
public sealed class BadgeCountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0 ? i.ToString(culture ?? CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
