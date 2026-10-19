using Agent.Ipc.Grpc;
using Core.Models;
using Google.Protobuf.WellKnownTypes;

namespace Agent.Service.Grpc;

/// <summary>
/// Mapeo entre los modelos de dominio de <c>Core</c> y los mensajes del
/// contrato gRPC <c>driveragent.proto</c>. Las fechas se normalizan a UTC.
/// </summary>
internal static class GrpcMappers
{
    internal static InstalledDriverMsg ToGrpc(InstalledDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        var message = new InstalledDriverMsg
        {
            DeviceId = driver.DeviceId,
            DeviceName = driver.DeviceName,
            Manufacturer = driver.Manufacturer,
            DriverVersion = driver.DriverVersion,
            DriverDate = ToTimestampUtc(driver.DriverDate),
            InfName = driver.InfName,
            Status = ToGrpc(driver.Status),
            DeviceClass = driver.DeviceClass
        };

        foreach (var hardwareId in driver.HardwareIds)
        {
            message.HardwareIds.Add(hardwareId.Value);
        }

        return message;
    }

    internal static DriverUpdateMsg ToGrpc(DriverUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        return new DriverUpdateMsg
        {
            UpdateId = update.UpdateId,
            Title = update.Title,
            Description = update.Description,
            NewVersion = update.NewVersion,
            CurrentVersion = update.CurrentVersion,
            HardwareId = update.MatchedHardwareId.Value,
            DeviceName = update.DeviceName,
            DownloadSizeBytes = update.DownloadSizeBytes,
            IsMandatory = update.IsMandatory,
            ReleaseDate = ToTimestampUtc(update.ReleaseDate)
        };
    }

    internal static OperationLogEntryMsg ToGrpc(OperationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new OperationLogEntryMsg
        {
            Id = entry.Id,
            OperationType = entry.OperationType,
            UpdateId = entry.UpdateId,
            DeviceName = entry.DeviceName,
            HardwareId = entry.HardwareId,
            DriverTitle = entry.DriverTitle,
            PreviousVersion = entry.PreviousVersion,
            NewVersion = entry.NewVersion,
            Success = entry.Success,
            ErrorMessage = entry.ErrorMessage ?? string.Empty,
            Timestamp = ToTimestampUtc(entry.Timestamp)
        };
    }

    internal static InstallProgressMsg ToProgress(
        string updateId,
        InstallProgress progress,
        int totalUpdates,
        int completedUpdates)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new InstallProgressMsg
        {
            UpdateId = updateId,
            PercentComplete = progress.PercentComplete,
            Phase = ToGrpc(progress.Phase),
            StatusMessage = progress.StatusMessage,
            Finished = false,
            TotalUpdates = totalUpdates,
            CompletedUpdates = completedUpdates
        };
    }

    internal static RollbackResultMsg ToGrpc(InstallResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new RollbackResultMsg
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage ?? string.Empty,
            InstalledVersion = result.InstalledVersion,
            PreviousVersion = result.PreviousVersion,
            RebootRequired = result.RebootRequired
        };
    }

    internal static InstallPhaseProto ToGrpc(InstallPhase phase) => phase switch
    {
        InstallPhase.Downloading => InstallPhaseProto.InstallPhaseDownloading,
        InstallPhase.Verifying => InstallPhaseProto.InstallPhaseVerifying,
        InstallPhase.CreatingRestorePoint => InstallPhaseProto.InstallPhaseCreatingRestorePoint,
        InstallPhase.Installing => InstallPhaseProto.InstallPhaseInstalling,
        InstallPhase.Validating => InstallPhaseProto.InstallPhaseValidating,
        InstallPhase.Complete => InstallPhaseProto.InstallPhaseComplete,
        InstallPhase.Failed => InstallPhaseProto.InstallPhaseFailed,
        InstallPhase.RollingBack => InstallPhaseProto.InstallPhaseRollingBack,
        _ => InstallPhaseProto.InstallPhaseUnspecified
    };

    private static DriverStatusProto ToGrpc(DriverStatus status) => status switch
    {
        DriverStatus.Ok => DriverStatusProto.DriverStatusOk,
        DriverStatus.Error => DriverStatusProto.DriverStatusError,
        DriverStatus.Degraded => DriverStatusProto.DriverStatusDegraded,
        DriverStatus.Unknown => DriverStatusProto.DriverStatusUnknown,
        _ => DriverStatusProto.DriverStatusUnspecified
    };

    private static Timestamp ToTimestampUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return Timestamp.FromDateTime(utc);
    }
}
