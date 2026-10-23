using Core.Models;

namespace Core.Services;

/// <summary>
/// Página oficial de soporte de drivers de cada fabricante de placa base.
/// Solo se enlazan dominios oficiales; la app nunca descarga desde aquí.
/// </summary>
public sealed record OemSupportEntry(string Vendor, Uri SupportUrl);

public static class OemSupportCatalog
{
    private static readonly Dictionary<string, OemSupportEntry> _entries =
        new Dictionary<string, OemSupportEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gigabyte"] = new("Gigabyte", new Uri("https://www.gigabyte.com/Support")),
            ["ASUS"] = new("ASUS", new Uri("https://www.asus.com/support/")),
            ["ASUSTeK COMPUTER INC."] = new("ASUS", new Uri("https://www.asus.com/support/")),
            ["MSI"] = new("MSI", new Uri("https://www.msi.com/support")),
            ["Micro-Star International Co., Ltd."] = new("MSI", new Uri("https://www.msi.com/support")),
            ["ASRock"] = new("ASRock", new Uri("https://www.asrock.com/support/index.asp")),
        };

    /// <summary>
    /// Busca el soporte oficial del fabricante indicado. Devuelve null si no hay entrada
    /// (en ese caso la UI debe mostrar el nombre tal cual y no enlazar nada).
    /// </summary>
    public static OemSupportEntry? Find(MotherboardInfo? board)
    {
        if (board is null || string.IsNullOrWhiteSpace(board.Manufacturer))
        {
            return null;
        }

        return _entries.TryGetValue(board.Manufacturer.Trim(), out var entry) ? entry : null;
    }
}
