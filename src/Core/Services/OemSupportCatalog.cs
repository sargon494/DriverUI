using Core.Models;

namespace Core.Services;

/// <summary>
/// Búsqueda oficial del modelo de placa base en el sitio de su fabricante.
/// Solo se enlazan dominios oficiales; la app nunca descarga desde aquí.
/// </summary>
public sealed record OemSupportEntry(string Vendor, Uri SupportUrl);

public static class OemSupportCatalog
{
    private sealed record OemSupportProvider(string Vendor, Func<string, Uri> CreateSearchUrl);

    private static readonly OemSupportProvider _gigabyte = new(
        "Gigabyte",
        model => new Uri($"https://www.gigabyte.com/search?keyword={Uri.EscapeDataString(model)}&type=All"));

    private static readonly OemSupportProvider _asus = new(
        "ASUS",
        model => new Uri($"https://www.asus.com/searchresult?searchType=products&searchKey={Uri.EscapeDataString(model)}&page=1"));

    private static readonly OemSupportProvider _msi = new(
        "MSI",
        model => new Uri($"https://www.msi.com/search?kw={Uri.EscapeDataString(model)}"));

    private static readonly OemSupportProvider _asrock = new(
        "ASRock",
        model => new Uri($"https://www.asrock.com/support/index.asp?Model={Uri.EscapeDataString(model)}"));

    private static readonly Dictionary<string, OemSupportProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gigabyte"] = _gigabyte,
            ["Gigabyte Technology Co., Ltd."] = _gigabyte,
            ["ASUS"] = _asus,
            ["ASUSTeK COMPUTER INC."] = _asus,
            ["MSI"] = _msi,
            ["Micro-Star International Co., Ltd."] = _msi,
            ["ASRock"] = _asrock,
        };

    /// <summary>
    /// Crea un enlace oficial de búsqueda para el modelo indicado. Devuelve null si el
    /// fabricante no está soportado o el modelo está vacío.
    /// </summary>
    public static OemSupportEntry? Find(MotherboardInfo? board)
    {
        if (board is null ||
            string.IsNullOrWhiteSpace(board.Manufacturer) ||
            string.IsNullOrWhiteSpace(board.Product))
        {
            return null;
        }

        if (!_providers.TryGetValue(board.Manufacturer.Trim(), out var provider))
        {
            return null;
        }

        return new OemSupportEntry(provider.Vendor, provider.CreateSearchUrl(board.Product.Trim()));
    }
}
