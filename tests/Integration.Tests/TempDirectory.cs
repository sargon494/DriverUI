namespace Integration.Tests;

/// <summary>
/// Carpeta temporal aislada por prueba. Se borra al terminar; si Windows aún
/// mantiene un archivo SQLite abierto por el pool de conexiones, el borrado
/// se omite sin fallar la prueba.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DriverUpdater.Integration",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Archivo todavía en uso (pool de SQLite); lo limpiará el sistema temporal.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorado: la carpeta es temporal y propia de la prueba.
        }
    }
}
