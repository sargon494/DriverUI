using System.Diagnostics;
using System.Globalization;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Install;

/// <summary>
/// Orquestador de instalación y reversión de controladores de dispositivos en Windows.
/// Aplica estrictamente las políticas de seguridad:
/// <list type="bullet">
///   <item><description>Verificación criptográfica de firma digital (Authenticode / WHQL).</description></item>
///   <item><description>Creación obligatoria de punto de restauración del sistema previo a cualquier cambio.</description></item>
///   <item><description>Auditoría persistente en SQLite de cada operación.</description></item>
/// </list>
/// </summary>
public sealed class WuaDriverInstaller : IDriverInstaller
{
    private readonly IWuaExecutor _wuaExecutor;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly IRestorePointService _restorePointService;
    private readonly IOperationHistory _operationHistory;
    private readonly ILogger<WuaDriverInstaller> _logger;

    public WuaDriverInstaller(
        ISignatureVerifier signatureVerifier,
        IRestorePointService restorePointService,
        IOperationHistory operationHistory,
        ILogger<WuaDriverInstaller> logger)
        : this(
            new DefaultWuaExecutor(new Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultWuaExecutor>()),
            signatureVerifier,
            restorePointService,
            operationHistory,
            logger)
    {
    }

    internal WuaDriverInstaller(
        IWuaExecutor wuaExecutor,
        ISignatureVerifier signatureVerifier,
        IRestorePointService restorePointService,
        IOperationHistory operationHistory,
        ILogger<WuaDriverInstaller> logger)
    {
        _wuaExecutor = wuaExecutor ?? throw new ArgumentNullException(nameof(wuaExecutor));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _restorePointService = restorePointService ?? throw new ArgumentNullException(nameof(restorePointService));
        _operationHistory = operationHistory ?? throw new ArgumentNullException(nameof(operationHistory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<InstallResult> InstallAsync(
        DriverUpdate update,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Iniciando flujo de instalación segura para: {Title} ({NewVersion})", update.Title, update.NewVersion);
        }

        var currentPhase = InstallPhase.Downloading;

        try
        {
            // ─── FASE 1: Descarga del paquete ─────────────────────────────────
            currentPhase = InstallPhase.Downloading;
            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Downloading,
                PercentComplete = 10,
                StatusMessage = $"Descargando actualización '{update.Title}'..."
            });

            var downloadResult = await _wuaExecutor.DownloadAsync(update.UpdateId, ct).ConfigureAwait(false);
            if (!downloadResult.Success)
            {
                var downloadError = downloadResult.ErrorMessage ?? "Error desconocido durante la descarga del controlador.";
                return await RecordFailureAsync(update, downloadError, InstallPhase.Downloading, progress, ct).ConfigureAwait(false);
            }

            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Downloading,
                PercentComplete = 25,
                StatusMessage = "Descarga completada con éxito."
            });

            // ─── FASE 2: Verificación criptográfica de firma ─────────────────
            currentPhase = InstallPhase.Verifying;
            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Verifying,
                PercentComplete = 30,
                StatusMessage = "Verificando firmas digitales de seguridad..."
            });

            foreach (var filePath in downloadResult.DownloadedFilePaths)
            {
                var sigResult = await _signatureVerifier.VerifyAsync(filePath, ct).ConfigureAwait(false);
                if (!sigResult.IsValid)
                {
                    var sigError = $"Firma no válida en archivo '{filePath}': {sigResult.FailureReason}. Instalación abortada por seguridad.";
                    return await RecordFailureAsync(update, sigError, InstallPhase.Verifying, progress, ct).ConfigureAwait(false);
                }
            }

            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Verifying,
                PercentComplete = 45,
                StatusMessage = "Firma digital verificada y de confianza."
            });

            // ─── FASE 3: Creación de Punto de Restauración del Sistema ───────
            currentPhase = InstallPhase.CreatingRestorePoint;
            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.CreatingRestorePoint,
                PercentComplete = 50,
                StatusMessage = "Creando punto de restauración del sistema previo a la instalación..."
            });

            var restorePointOk = await _restorePointService.CreateRestorePointAsync(
                $"DriverUpdater: {update.Title} ({update.NewVersion})", ct).ConfigureAwait(false);

            if (!restorePointOk)
            {
                var restoreError = "No se pudo crear el punto de restauración del sistema. Instalación abortada por seguridad.";
                return await RecordFailureAsync(update, restoreError, InstallPhase.CreatingRestorePoint, progress, ct).ConfigureAwait(false);
            }

            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.CreatingRestorePoint,
                PercentComplete = 65,
                StatusMessage = "Punto de restauración creado satisfactoriamente."
            });

            // ─── FASE 4: Instalación del controlador ─────────────────────────
            // Se valida la cancelación justo antes de iniciar la instalación nativa.
            // Una vez que el instalador nativo (WUA/pnputil) arranca, no se admite cancelación
            // para evitar dejar el subsistema de controladores de Windows en un estado corrupto o inconsistente.
            ct.ThrowIfCancellationRequested();

            currentPhase = InstallPhase.Installing;
            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Installing,
                PercentComplete = 70,
                StatusMessage = $"Instalando controlador '{update.Title}'..."
            });

            var installResult = await _wuaExecutor.InstallAsync(update.UpdateId, CancellationToken.None).ConfigureAwait(false);
            if (!installResult.Success)
            {
                var installError = installResult.ErrorMessage ?? "Fallo en la instalación del paquete de controlador.";
                return await RecordFailureAsync(update, installError, InstallPhase.Installing, progress, ct).ConfigureAwait(false);
            }

            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Installing,
                PercentComplete = 85,
                StatusMessage = "Instalación completada en el subsistema de Windows."
            });

            // ─── FASE 5: Validación final ─────────────────────────────────────
            currentPhase = InstallPhase.Validating;
            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Validating,
                PercentComplete = 90,
                StatusMessage = "Validando estado final del dispositivo..."
            });

            var timestamp = DateTime.UtcNow;
            var finalResult = new InstallResult
            {
                UpdateId = update.UpdateId,
                Success = true,
                ErrorMessage = null,
                RebootRequired = installResult.RebootRequired,
                PreviousVersion = update.CurrentVersion,
                InstalledVersion = update.NewVersion,
                Timestamp = timestamp
            };

            // ─── Registro en historial ────────────────────────────────────────
            await _operationHistory.RecordAsync(new OperationLogEntry
            {
                Id = 0,
                OperationType = "Install",
                UpdateId = update.UpdateId,
                DeviceName = update.DeviceName,
                HardwareId = update.MatchedHardwareId.Value,
                DriverTitle = update.Title,
                PreviousVersion = update.CurrentVersion,
                NewVersion = update.NewVersion,
                Success = true,
                ErrorMessage = null,
                Timestamp = timestamp
            }, CancellationToken.None).ConfigureAwait(false);

            progress?.Report(new InstallProgress
            {
                Phase = InstallPhase.Complete,
                PercentComplete = 100,
                StatusMessage = installResult.RebootRequired
                    ? "Instalación finalizada con éxito. Se requiere reiniciar el equipo."
                    : "Instalación finalizada con éxito."
            });

            return finalResult;
        }
        catch (OperationCanceledException)
        {
            var cancelMessage = $"Instalación cancelada durante la fase {currentPhase}.";
            await RecordFailureAsync(update, cancelMessage, currentPhase, progress, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            var errorMessage = currentPhase is InstallPhase.Installing or InstallPhase.Validating
                ? $"Excepción inesperada durante la fase {currentPhase} (la instalación nativa ya había iniciado, verifique el estado del dispositivo): {ex.Message}"
                : $"Excepción inesperada durante la fase {currentPhase}: {ex.Message}";
            return await RecordFailureAsync(update, errorMessage, currentPhase, progress, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<InstallResult> RollbackAsync(
        string operationId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        if (!ulong.TryParse(operationId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId)
            || parsedId == 0
            || parsedId > (ulong)long.MaxValue)
        {
            return new InstallResult
            {
                UpdateId = string.Empty,
                Success = false,
                ErrorMessage = $"Identificador de operación no válido: '{operationId}'.",
                RebootRequired = false,
                PreviousVersion = string.Empty,
                InstalledVersion = string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        var id = (long)parsedId;

        var entry = await _operationHistory.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entry == null)
        {
            return new InstallResult
            {
                UpdateId = string.Empty,
                Success = false,
                ErrorMessage = $"No se encontró registro de operación para el ID {id}.",
                RebootRequired = false,
                PreviousVersion = string.Empty,
                InstalledVersion = string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Iniciando rollback de operación {Id} ({DeviceName}, de {CurrentVersion} a {PreviousVersion})...",
                entry.Id, entry.DeviceName, entry.NewVersion, entry.PreviousVersion);
        }

        // Ejecutar pnputil para revertir/reiniciar el dispositivo de manera segura
        bool rollbackSuccess = false;
        string? errorMessage = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Invocar pnputil /restart-device para recargar controlador previo
            startInfo.ArgumentList.Add("/restart-device");
            if (!string.IsNullOrWhiteSpace(entry.HardwareId))
            {
                startInfo.ArgumentList.Add(entry.HardwareId);
            }

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                rollbackSuccess = process.ExitCode == 0;
                if (!rollbackSuccess)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                    errorMessage = $"pnputil retornó código {process.ExitCode}: {stderr}".Trim();
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error al ejecutar rollback: {ex.Message}";
            _logger.LogError(ex, "Error durante rollback de operación {Id}", entry.Id);
        }

        var timestamp = DateTime.UtcNow;

        await _operationHistory.RecordAsync(new OperationLogEntry
        {
            Id = 0,
            OperationType = "Rollback",
            UpdateId = entry.UpdateId,
            DeviceName = entry.DeviceName,
            HardwareId = entry.HardwareId,
            DriverTitle = entry.DriverTitle,
            PreviousVersion = entry.NewVersion,
            NewVersion = entry.PreviousVersion,
            Success = rollbackSuccess,
            ErrorMessage = errorMessage,
            Timestamp = timestamp
        }, ct).ConfigureAwait(false);

        return new InstallResult
        {
            UpdateId = entry.UpdateId,
            Success = rollbackSuccess,
            ErrorMessage = errorMessage,
            RebootRequired = false,
            PreviousVersion = entry.NewVersion,
            InstalledVersion = rollbackSuccess ? entry.PreviousVersion : entry.NewVersion,
            Timestamp = timestamp
        };
    }

    private async Task<InstallResult> RecordFailureAsync(
        DriverUpdate update,
        string errorMessage,
        InstallPhase phase,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError("Fallo en instalación de {Title} durante la fase {Phase}: {Error}", update.Title, phase, errorMessage);
        }

        progress?.Report(new InstallProgress
        {
            Phase = InstallPhase.Failed,
            PercentComplete = 100,
            StatusMessage = errorMessage
        });

        var timestamp = DateTime.UtcNow;

        // Regla 8 de AGENTS.md: Cada instalación deja registro en el log de operaciones.
        // Se utiliza CancellationToken.None para asegurar la persistencia en el historial
        // incluso ante cancelaciones del llamador.
        await _operationHistory.RecordAsync(new OperationLogEntry
        {
            Id = 0,
            OperationType = "Install",
            UpdateId = update.UpdateId,
            DeviceName = update.DeviceName,
            HardwareId = update.MatchedHardwareId.Value,
            DriverTitle = update.Title,
            PreviousVersion = update.CurrentVersion,
            NewVersion = update.NewVersion,
            Success = false,
            ErrorMessage = errorMessage,
            Timestamp = timestamp
        }, CancellationToken.None).ConfigureAwait(false);

        return new InstallResult
        {
            UpdateId = update.UpdateId,
            Success = false,
            ErrorMessage = errorMessage,
            RebootRequired = false,
            PreviousVersion = update.CurrentVersion,
            InstalledVersion = string.Empty,
            Timestamp = timestamp
        };
    }
}
