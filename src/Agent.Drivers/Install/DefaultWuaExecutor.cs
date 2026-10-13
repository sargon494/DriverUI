using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Install;

/// <summary>
/// Implementación por defecto de <see cref="IWuaExecutor"/> que interactúa con
/// los objetos COM de Windows Update Agent (<c>Microsoft.Update.Session</c>).
/// </summary>
internal sealed class DefaultWuaExecutor : IWuaExecutor
{
    private const string WuaProgId = "Microsoft.Update.Session";
    private const string UpdateCollectionProgId = "Microsoft.Update.UpdateColl";
    private readonly ILogger<DefaultWuaExecutor> _logger;

    public DefaultWuaExecutor(ILogger<DefaultWuaExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<WuaDownloadResult> DownloadAsync(string updateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updateId);

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var sessionType = Type.GetTypeFromProgID(WuaProgId)
                ?? throw new InvalidOperationException("No se pudo obtener el tipo COM para 'Microsoft.Update.Session'.");

            object? rawSession = null;
            object? rawSearcher = null;
            object? rawSearchResult = null;
            object? rawUpdateColl = null;
            object? rawDownloader = null;

            try
            {
                rawSession = Activator.CreateInstance(sessionType)
                    ?? throw new InvalidOperationException("No se pudo instanciar 'Microsoft.Update.Session'.");

                dynamic session = rawSession;
                rawSearcher = session.CreateUpdateSearcher();
                dynamic searcher = rawSearcher!;

                ct.ThrowIfCancellationRequested();

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Buscando actualización {UpdateId} en WUA para descargar...", updateId);
                }

                // Criterio de búsqueda en WUA
                rawSearchResult = searcher.Search($"UpdateID='{updateId}'");
                dynamic searchResult = rawSearchResult!;

                int count = searchResult.Updates.Count;
                if (count == 0)
                {
                    return new WuaDownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"No se encontró la actualización con ID '{updateId}' en Windows Update.",
                        DownloadedFilePaths = []
                    };
                }

                dynamic targetUpdate = searchResult.Updates.Item(0);

                var collType = Type.GetTypeFromProgID(UpdateCollectionProgId)
                    ?? throw new InvalidOperationException("No se pudo obtener el tipo COM para 'Microsoft.Update.UpdateColl'.");

                rawUpdateColl = Activator.CreateInstance(collType)
                    ?? throw new InvalidOperationException("No se pudo instanciar 'Microsoft.Update.UpdateColl'.");

                dynamic updateColl = rawUpdateColl;
                updateColl.Add(targetUpdate);

                rawDownloader = session.CreateUpdateDownloader();
                dynamic downloader = rawDownloader!;
                downloader.Updates = updateColl;

                ct.ThrowIfCancellationRequested();

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Descargando actualización {UpdateId}...", updateId);
                }

                dynamic downloadResult = downloader.Download();
                int resultCode = downloadResult.ResultCode; // 2 = orcSucceeded

                if (resultCode == 2)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Descarga de {UpdateId} completada con éxito.", updateId);
                    }

                    return new WuaDownloadResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        DownloadedFilePaths = []
                    };
                }

                var errorMsg = $"La descarga de WUA falló con código de resultado: {resultCode}";
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("{Error}", errorMsg);
                }

                return new WuaDownloadResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    DownloadedFilePaths = []
                };
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error durante la descarga de {UpdateId}", updateId);
                }

                return new WuaDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DownloadedFilePaths = []
                };
            }
            finally
            {
                ReleaseComObject(rawDownloader);
                ReleaseComObject(rawUpdateColl);
                ReleaseComObject(rawSearchResult);
                ReleaseComObject(rawSearcher);
                ReleaseComObject(rawSession);
            }
        }, ct);
    }

    /// <inheritdoc/>
    public Task<WuaInstallExecutionResult> InstallAsync(string updateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updateId);

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var sessionType = Type.GetTypeFromProgID(WuaProgId)
                ?? throw new InvalidOperationException("No se pudo obtener el tipo COM para 'Microsoft.Update.Session'.");

            object? rawSession = null;
            object? rawSearcher = null;
            object? rawSearchResult = null;
            object? rawUpdateColl = null;
            object? rawInstaller = null;

            try
            {
                rawSession = Activator.CreateInstance(sessionType)
                    ?? throw new InvalidOperationException("No se pudo instanciar 'Microsoft.Update.Session'.");

                dynamic session = rawSession;
                rawSearcher = session.CreateUpdateSearcher();
                dynamic searcher = rawSearcher!;

                ct.ThrowIfCancellationRequested();

                rawSearchResult = searcher.Search($"UpdateID='{updateId}'");
                dynamic searchResult = rawSearchResult!;

                int count = searchResult.Updates.Count;
                if (count == 0)
                {
                    return new WuaInstallExecutionResult
                    {
                        Success = false,
                        RebootRequired = false,
                        ErrorMessage = $"No se encontró la actualización con ID '{updateId}' en Windows Update."
                    };
                }

                dynamic targetUpdate = searchResult.Updates.Item(0);

                var collType = Type.GetTypeFromProgID(UpdateCollectionProgId)
                    ?? throw new InvalidOperationException("No se pudo obtener el tipo COM para 'Microsoft.Update.UpdateColl'.");

                rawUpdateColl = Activator.CreateInstance(collType)
                    ?? throw new InvalidOperationException("No se pudo instanciar 'Microsoft.Update.UpdateColl'.");

                dynamic updateColl = rawUpdateColl;
                updateColl.Add(targetUpdate);

                rawInstaller = session.CreateUpdateInstaller();
                dynamic installer = rawInstaller!;
                installer.Updates = updateColl;

                ct.ThrowIfCancellationRequested();

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Instalando actualización {UpdateId} mediante WUA...", updateId);
                }

                dynamic installResult = installer.Install();
                int resultCode = installResult.ResultCode; // 2 = orcSucceeded
                bool rebootRequired = installResult.RebootRequired;

                if (resultCode == 2)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "Instalación de {UpdateId} completada exitosamente. Reinicio requerido: {RebootRequired}",
                            updateId, rebootRequired);
                    }

                    return new WuaInstallExecutionResult
                    {
                        Success = true,
                        RebootRequired = rebootRequired,
                        ErrorMessage = null
                    };
                }

                var errorMsg = $"La instalación de WUA falló con código de resultado: {resultCode}";
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("{Error}", errorMsg);
                }

                return new WuaInstallExecutionResult
                {
                    Success = false,
                    RebootRequired = rebootRequired,
                    ErrorMessage = errorMsg
                };
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error durante la instalación de {UpdateId}", updateId);
                }

                return new WuaInstallExecutionResult
                {
                    Success = false,
                    RebootRequired = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                ReleaseComObject(rawInstaller);
                ReleaseComObject(rawUpdateColl);
                ReleaseComObject(rawSearchResult);
                ReleaseComObject(rawSearcher);
                ReleaseComObject(rawSession);
            }
        }, ct);
    }

    private static void ReleaseComObject(object? obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            try
            {
                Marshal.ReleaseComObject(obj);
            }
            catch
            {
                // Ignorar excepciones al liberar COM en teardown
            }
        }
    }
}
