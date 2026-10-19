using Agent.Drivers.Install;
using Core.Interfaces;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Agent.Drivers.Tests;

public sealed class WuaDriverInstallerTests
{
    private readonly IWuaExecutor _wuaExecutor = Substitute.For<IWuaExecutor>();
    private readonly ISignatureVerifier _signatureVerifier = Substitute.For<ISignatureVerifier>();
    private readonly IRestorePointService _restorePointService = Substitute.For<IRestorePointService>();
    private readonly IOperationHistory _operationHistory = Substitute.For<IOperationHistory>();

    private readonly WuaDriverInstaller _installer;

    public WuaDriverInstallerTests()
    {
        _installer = new WuaDriverInstaller(
            _wuaExecutor,
            _signatureVerifier,
            _restorePointService,
            _operationHistory,
            NullLogger<WuaDriverInstaller>.Instance);
    }

    private static DriverUpdate CreateSampleUpdate() => new()
    {
        UpdateId = "upd-test-1",
        Title = "Intel Graphics Driver",
        Description = "Display driver update",
        NewVersion = "31.0.101.4500",
        CurrentVersion = "30.0.100.9999",
        MatchedHardwareId = new HardwareId { Value = @"PCI\VEN_8086&DEV_9A49" },
        DeviceName = "Intel(R) Iris(R) Xe Graphics",
        DownloadSizeBytes = 50_000_000,
        IsMandatory = false,
        ReleaseDate = DateTime.UtcNow
    };

    [Fact]
    public async Task InstallAsync_Should_Throw_When_Update_Is_Null()
    {
        var act = () => _installer.InstallAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InstallAsync_Should_Complete_Successfully_When_All_Steps_Succeed()
    {
        var update = CreateSampleUpdate();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaDownloadResult
            {
                Success = true,
                ErrorMessage = null,
                DownloadedFilePaths = [@"C:\temp\driver.sys"]
            });

        _signatureVerifier.VerifyAsync(@"C:\temp\driver.sys", Arg.Any<CancellationToken>())
            .Returns(new SignatureVerificationResult
            {
                IsValid = true,
                SignerName = "Microsoft Windows Hardware Compatibility Publisher",
                FailureReason = null,
                IsWhql = true
            });

        _restorePointService.CreateRestorePointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _wuaExecutor.InstallAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaInstallExecutionResult
            {
                Success = true,
                RebootRequired = false,
                ErrorMessage = null
            });

        var progressReports = new List<InstallProgress>();
        var progress = new Progress<InstallProgress>(p => progressReports.Add(p));

        var result = await _installer.InstallAsync(update, progress);

        result.Success.Should().BeTrue();
        result.UpdateId.Should().Be(update.UpdateId);
        result.InstalledVersion.Should().Be(update.NewVersion);
        result.RebootRequired.Should().BeFalse();

        await _operationHistory.Received(1).RecordAsync(
            Arg.Is<OperationLogEntry>(e =>
                e.OperationType == "Install" &&
                e.UpdateId == update.UpdateId &&
                e.Success &&
                e.NewVersion == update.NewVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAsync_Should_Abort_And_Record_Failure_When_RestorePoint_Fails()
    {
        // Regla 2 de seguridad: Siempre crear un punto de restauración antes de instalar. Si falla, abortar.
        var update = CreateSampleUpdate();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaDownloadResult
            {
                Success = true,
                ErrorMessage = null,
                DownloadedFilePaths = []
            });

        _restorePointService.CreateRestorePointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false); // Falla la creación del punto de restauración

        var result = await _installer.InstallAsync(update);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("punto de restauración");

        // NO se debe llamar a la instalación
        await _wuaExecutor.DidNotReceive().InstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Debe registrar el fallo en el historial
        await _operationHistory.Received(1).RecordAsync(
            Arg.Is<OperationLogEntry>(e =>
                e.OperationType == "Install" &&
                e.UpdateId == update.UpdateId &&
                !e.Success &&
                e.ErrorMessage != null &&
                e.ErrorMessage.Contains("punto de restauración")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAsync_Should_Abort_When_Signature_Is_Invalid()
    {
        // Regla 1 de seguridad: Nunca instalar un driver sin verificar su firma digital.
        var update = CreateSampleUpdate();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaDownloadResult
            {
                Success = true,
                ErrorMessage = null,
                DownloadedFilePaths = [@"C:\temp\untrusted.sys"]
            });

        _signatureVerifier.VerifyAsync(@"C:\temp\untrusted.sys", Arg.Any<CancellationToken>())
            .Returns(new SignatureVerificationResult
            {
                IsValid = false,
                SignerName = null,
                FailureReason = "Firma no confiable o no válida.",
                IsWhql = false
            });

        var result = await _installer.InstallAsync(update);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Firma no válida");

        // NO debe intentar crear restore point ni instalar
        await _restorePointService.DidNotReceive().CreateRestorePointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _wuaExecutor.DidNotReceive().InstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Debe registrar fallo en el historial
        await _operationHistory.Received(1).RecordAsync(
            Arg.Is<OperationLogEntry>(e =>
                e.OperationType == "Install" &&
                !e.Success &&
                e.ErrorMessage != null &&
                e.ErrorMessage.Contains("Firma no válida")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAsync_Should_Abort_When_Download_Fails()
    {
        var update = CreateSampleUpdate();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaDownloadResult
            {
                Success = false,
                ErrorMessage = "Error de red al descargar",
                DownloadedFilePaths = []
            });

        var result = await _installer.InstallAsync(update);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Error de red al descargar");

        await _restorePointService.DidNotReceive().CreateRestorePointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _wuaExecutor.DidNotReceive().InstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackAsync_Should_Fail_When_Operation_Not_Found()
    {
        _operationHistory.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((OperationLogEntry?)null);

        var result = await _installer.RollbackAsync("999");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No se encontró registro");
    }

    [Fact]
    public async Task RollbackAsync_Should_Fail_When_OperationId_Is_Not_A_Number()
    {
        var result = await _installer.RollbackAsync("abc-invalid");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no válido");
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-999")]
    [InlineData("+42")]
    [InlineData(" 123 ")]
    [InlineData("0")]
    public async Task RollbackAsync_Should_Fail_When_OperationId_Is_Negative_Signed_Or_Zero(string invalidId)
    {
        var result = await _installer.RollbackAsync(invalidId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no válido");
    }

    [Fact]
    public async Task InstallAsync_Should_Record_Failure_And_Throw_When_Cancelled_During_Download()
    {
        var update = CreateSampleUpdate();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns<Task<WuaDownloadResult>>(_ => Task.FromCanceled<WuaDownloadResult>(cts.Token));

        var act = () => _installer.InstallAsync(update, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // Regla 8 de AGENTS.md: Registro obligatorio en el historial ante cancelación
        await _operationHistory.Received(1).RecordAsync(
            Arg.Is<OperationLogEntry>(e =>
                e.OperationType == "Install" &&
                e.UpdateId == update.UpdateId &&
                !e.Success &&
                e.ErrorMessage != null &&
                e.ErrorMessage.Contains("cancelada")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAsync_Should_Record_Failure_And_Throw_When_Cancelled_Before_Native_Install()
    {
        var update = CreateSampleUpdate();
        using var cts = new CancellationTokenSource();

        _wuaExecutor.DownloadAsync(update.UpdateId, Arg.Any<CancellationToken>())
            .Returns(new WuaDownloadResult
            {
                Success = true,
                ErrorMessage = null,
                DownloadedFilePaths = []
            });

        _restorePointService.CreateRestorePointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel(); // Cancelar tras el punto de restauración
                return Task.FromResult(true);
            });

        var act = () => _installer.InstallAsync(update, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // La instalación nativa de WUA NO debe haberse llamado
        await _wuaExecutor.DidNotReceive().InstallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // El fallo debe haberse registrado en el historial de operaciones
        await _operationHistory.Received(1).RecordAsync(
            Arg.Is<OperationLogEntry>(e =>
                e.OperationType == "Install" &&
                e.UpdateId == update.UpdateId &&
                !e.Success &&
                e.ErrorMessage != null &&
                e.ErrorMessage.Contains("cancelada")),
            Arg.Any<CancellationToken>());
    }
}
