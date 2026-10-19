using Agent.Ipc.Grpc;
using Agent.Service.Grpc;
using Core.Models;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;

namespace Agent.Service.Tests;

public class GrpcMappersTests
{
    [Fact]
    public void ToGrpc_InstalledDriver_Should_Map_All_Fields()
    {
        var driver = new InstalledDriver
        {
            DeviceId = @"PCI\VEN_8086&DEV_9A49",
            DeviceName = "Intel(R) Iris Xe Graphics",
            Manufacturer = "Intel Corporation",
            DriverVersion = "31.0.101.2115",
            DriverDate = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            InfName = "iigd_dch.inf",
            HardwareIds = [new HardwareId { Value = @"PCI\VEN_8086&DEV_9A49&REV_03" }, new HardwareId { Value = @"PCI\VEN_8086&DEV_9A49" }],
            Status = DriverStatus.Ok,
            DeviceClass = "DISPLAY"
        };

        var message = GrpcMappers.ToGrpc(driver);

        message.DeviceId.Should().Be(driver.DeviceId);
        message.DeviceName.Should().Be(driver.DeviceName);
        message.Manufacturer.Should().Be(driver.Manufacturer);
        message.DriverVersion.Should().Be(driver.DriverVersion);
        message.DriverDate.Should().NotBeNull();
        message.DriverDate!.Should().Be(Timestamp.FromDateTime(driver.DriverDate));
        message.InfName.Should().Be(driver.InfName);
        message.HardwareIds.Should().BeEquivalentTo(driver.HardwareIds.Select(id => id.Value));
        message.Status.Should().Be(DriverStatusProto.DriverStatusOk);
        message.DeviceClass.Should().Be(driver.DeviceClass);
    }

    [Fact]
    public void ToGrpc_DriverUpdate_Should_Map_All_Fields()
    {
        var update = new DriverUpdate
        {
            UpdateId = "12345678-90ab-cdef-1234-567890abcdef",
            Title = "Intel - Extension - 2.1.10103.18",
            Description = "Actualización de controlador de gráficos.",
            NewVersion = "2.1.10103.18",
            CurrentVersion = "2.0.10109.0",
            MatchedHardwareId = new HardwareId { Value = @"PCI\VEN_8086&DEV_9A49" },
            DeviceName = "Intel(R) Iris Xe Graphics",
            DownloadSizeBytes = 512_000_000L,
            IsMandatory = false,
            ReleaseDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var message = GrpcMappers.ToGrpc(update);

        message.UpdateId.Should().Be(update.UpdateId);
        message.Title.Should().Be(update.Title);
        message.Description.Should().Be(update.Description);
        message.NewVersion.Should().Be(update.NewVersion);
        message.CurrentVersion.Should().Be(update.CurrentVersion);
        message.HardwareId.Should().Be(update.MatchedHardwareId.Value);
        message.DeviceName.Should().Be(update.DeviceName);
        message.DownloadSizeBytes.Should().Be(update.DownloadSizeBytes);
        message.IsMandatory.Should().Be(update.IsMandatory);
        message.ReleaseDate.Should().NotBeNull();
        message.ReleaseDate!.Should().Be(Timestamp.FromDateTime(update.ReleaseDate));
    }

    [Fact]
    public void ToTimestampUtc_Should_Normalize_Unspecified_And_Local_Kinds()
    {
        var unspecified = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var local = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Local);

        var message = GrpcMappers.ToGrpc(new OperationLogEntry
        {
            Id = 1,
            OperationType = "Install",
            UpdateId = "u-1",
            DeviceName = "device",
            HardwareId = @"PCI\VEN_8086",
            DriverTitle = "title",
            PreviousVersion = "1.0",
            NewVersion = "2.0",
            Success = true,
            ErrorMessage = null,
            Timestamp = unspecified
        });

        message.Timestamp.Should().Be(Timestamp.FromDateTime(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)));

        var mappedLocal = GrpcMappers.ToGrpc(new OperationLogEntry
        {
            Id = 1,
            OperationType = "Install",
            UpdateId = "u-1",
            DeviceName = "device",
            HardwareId = @"PCI\VEN_8086",
            DriverTitle = "title",
            PreviousVersion = "1.0",
            NewVersion = "2.0",
            Success = true,
            ErrorMessage = null,
            Timestamp = local
        });

        mappedLocal.Timestamp.Should().Be(Timestamp.FromDateTime(local.ToUniversalTime()));
    }

    [Theory]
    [InlineData(InstallPhase.Downloading, InstallPhaseProto.InstallPhaseDownloading)]
    [InlineData(InstallPhase.Verifying, InstallPhaseProto.InstallPhaseVerifying)]
    [InlineData(InstallPhase.CreatingRestorePoint, InstallPhaseProto.InstallPhaseCreatingRestorePoint)]
    [InlineData(InstallPhase.Installing, InstallPhaseProto.InstallPhaseInstalling)]
    [InlineData(InstallPhase.Validating, InstallPhaseProto.InstallPhaseValidating)]
    [InlineData(InstallPhase.Complete, InstallPhaseProto.InstallPhaseComplete)]
    [InlineData(InstallPhase.Failed, InstallPhaseProto.InstallPhaseFailed)]
    [InlineData(InstallPhase.RollingBack, InstallPhaseProto.InstallPhaseRollingBack)]
    public void ToGrpc_InstallPhase_Should_Map_All_Values(InstallPhase phase, InstallPhaseProto expected)
    {
        GrpcMappers.ToGrpc(phase).Should().Be(expected);
    }

    [Fact]
    public void ToProgress_Should_Map_Progress_Without_Finishing()
    {
        var progress = new InstallProgress
        {
            PercentComplete = 45,
            StatusMessage = "Verificando firmas...",
            Phase = InstallPhase.Verifying
        };

        var message = GrpcMappers.ToProgress("u-1", progress, totalUpdates: 3, completedUpdates: 1);

        message.UpdateId.Should().Be("u-1");
        message.PercentComplete.Should().Be(45);
        message.Phase.Should().Be(InstallPhaseProto.InstallPhaseVerifying);
        message.StatusMessage.Should().Be("Verificando firmas...");
        message.Finished.Should().BeFalse();
        message.TotalUpdates.Should().Be(3);
        message.CompletedUpdates.Should().Be(1);
    }
}
