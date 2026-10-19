using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Agent.Ipc;
using Agent.Ipc.Grpc;
using Agent.Service.Grpc;
using Agent.Service.Ipc;
using Agent.Service.Operations;
using Core.Interfaces;
using Core.Models;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Agent.Service.Tests;

public class DriverAgentGrpcServiceTests
{
    private static InstalledDriver CreateInstalledDriver(string deviceId = "PCI\\DEV-1") => new()
    {
        DeviceId = deviceId,
        DeviceName = "Dispositivo de prueba",
        Manufacturer = "Acme Corp",
        DriverVersion = "1.0.0.0",
        DriverDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        InfName = "acme.inf",
        HardwareIds = [new HardwareId { Value = "PCI\\VEN_0001&DEV_0001" }],
        Status = DriverStatus.Ok,
        DeviceClass = "SYSTEM"
    };

    private static DriverUpdate CreateUpdate(string updateId, string deviceName = "Dispositivo de prueba") => new()
    {
        UpdateId = updateId,
        Title = $"Actualización {updateId}",
        Description = "Actualización simulada para pruebas.",
        NewVersion = "2.0.0.0",
        CurrentVersion = "1.0.0.0",
        MatchedHardwareId = new HardwareId { Value = "PCI\\VEN_0001&DEV_0001" },
        DeviceName = deviceName,
        DownloadSizeBytes = 1024,
        IsMandatory = false,
        ReleaseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static InstallResult CreateInstallResult(string updateId, bool success, bool rebootRequired = false) => new()
    {
        UpdateId = updateId,
        Success = success,
        ErrorMessage = success ? null : "Error simulado de instalación.",
        RebootRequired = rebootRequired,
        PreviousVersion = "1.0.0.0",
        InstalledVersion = success ? "2.0.0.0" : "1.0.0.0",
        Timestamp = DateTime.UtcNow
    };

    private static async IAsyncEnumerable<DriverUpdate> ToAsyncEnumerable(IReadOnlyList<DriverUpdate> updates)
    {
        foreach (var update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }

    private static async Task<List<T>> CollectAllAsync<T>(IAsyncStreamReader<T> stream)
    {
        var items = new List<T>();
        await foreach (var item in stream.ReadAllAsync())
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly CancellationToken _cancellationToken;
        public TestServerCallContext(CancellationToken cancellationToken = default) => _cancellationToken = cancellationToken;
        protected override string MethodCore => "Install";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => _cancellationToken;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    private sealed class AgentHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly GrpcChannel _channel;

        public AgentHarness(
            WebApplication app,
            GrpcChannel channel,
            IDriverInventory inventory,
            IUpdateSource updateSource,
            IDriverInstaller installer,
            IOperationHistory history)
        {
            _app = app;
            _channel = channel;
            Inventory = inventory;
            UpdateSource = updateSource;
            Installer = installer;
            History = history;
            Client = new DriverAgent.DriverAgentClient(channel);
        }

        public IDriverInventory Inventory { get; }

        public IUpdateSource UpdateSource { get; }

        public IDriverInstaller Installer { get; }

        public IOperationHistory History { get; }

        public DriverAgent.DriverAgentClient Client { get; }

        public static async Task<AgentHarness> StartAsync(Action<IServiceCollection>? extraSetup = null)
        {
            var pipeName = $"driverupdater-tests-{Guid.NewGuid():N}";

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrelOptions =>
            {
                kestrelOptions.ListenNamedPipe(pipeName, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();
            builder.Services.AddSingleton(Substitute.For<IDriverInventory>());
            builder.Services.AddSingleton(Substitute.For<IUpdateSource>());
            builder.Services.AddSingleton(Substitute.For<IDriverInstaller>());
            builder.Services.AddSingleton(Substitute.For<IOperationHistory>());
            builder.Services.AddSingleton<IOperationQueue, OperationQueue>();
            extraSetup?.Invoke(builder.Services);

            var app = builder.Build();
            app.MapGrpcService<DriverAgentGrpcService>();
            await app.StartAsync();

            var channel = DriverAgentChannel.CreateChannel(pipeName);
            var harness = new AgentHarness(
                app,
                channel,
                app.Services.GetRequiredService<IDriverInventory>(),
                app.Services.GetRequiredService<IUpdateSource>(),
                app.Services.GetRequiredService<IDriverInstaller>(),
                app.Services.GetRequiredService<IOperationHistory>());

            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await _app.DisposeAsync();
        }
    }

    public class Scan
    {
        [Fact]
        public async Task Should_Return_Mapped_Drivers_And_Register_Last_Scan()
        {
            await using var harness = await AgentHarness.StartAsync();
            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver(), CreateInstalledDriver("PCI\\DEV-2") });

            var response = await harness.Client.ScanAsync(new ScanRequest());

            response.Drivers.Should().HaveCount(2);
            response.Drivers[0].DeviceName.Should().Be("Dispositivo de prueba");
            response.Drivers[0].DriverVersion.Should().Be("1.0.0.0");

            var status = await harness.Client.GetStatusAsync(new StatusRequest());
            status.LastScan.Should().NotBeNull();
            status.OperationInProgress.Should().BeFalse();
        }
    }

    public class CheckUpdates
    {
        [Fact]
        public async Task Should_Stream_Available_Updates()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            using var call = harness.Client.CheckUpdates(new CheckUpdatesRequest());
            var streamed = await CollectAllAsync(call.ResponseStream);

            streamed.Should().HaveCount(2);
            streamed.Select(u => u.UpdateId).Should().Equal("update-1", "update-2");
            streamed[0].NewVersion.Should().Be("2.0.0.0");
        }

        [Fact]
        public async Task Should_Release_Operation_Queue_Before_Streaming_To_Client()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-1") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            using var call = harness.Client.CheckUpdates(new CheckUpdatesRequest());

            var hasNext = await call.ResponseStream.MoveNext(default);
            hasNext.Should().BeTrue();

            var status = await harness.Client.GetStatusAsync(new StatusRequest());
            status.OperationInProgress.Should().BeFalse();
        }
    }

    public class Install
    {
        [Fact]
        public async Task Should_Stream_Progress_And_Result_For_Each_Update()
        {
            await using var harness = await AgentHarness.StartAsync();
            var update = CreateUpdate("update-1");

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(new List<DriverUpdate> { update }));

            harness.Installer
                .InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var progress = callInfo.ArgAt<IProgress<InstallProgress>>(1);
                    progress?.Report(new InstallProgress { PercentComplete = 10, Phase = InstallPhase.Downloading, StatusMessage = "Descargando..." });
                    progress?.Report(new InstallProgress { PercentComplete = 50, Phase = InstallPhase.CreatingRestorePoint, StatusMessage = "Creando punto de restauración..." });
                    progress?.Report(new InstallProgress { PercentComplete = 90, Phase = InstallPhase.Validating, StatusMessage = "Validando..." });
                    return CreateInstallResult("update-1", success: true, rebootRequired: true);
                });

            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "update-1" } });
            var messages = await CollectAllAsync(call.ResponseStream);

            var progressMessages = messages.Where(m => !m.Finished).ToList();
            progressMessages.Should().HaveCount(3);
            progressMessages.Select(m => m.PercentComplete).Should().Equal(10, 50, 90);
            progressMessages.All(m => m.UpdateId == "update-1").Should().BeTrue();
            progressMessages.All(m => m.TotalUpdates == 1).Should().BeTrue();

            messages.Should().ContainSingle(m => m.Finished);
            var result = messages.Single(m => m.Finished);
            result.Success.Should().BeTrue();
            result.RebootRequired.Should().BeTrue();
            result.CompletedUpdates.Should().Be(1);

            var status = await harness.Client.GetStatusAsync(new StatusRequest());
            status.PendingReboot.Should().BeTrue();
            status.OperationInProgress.Should().BeFalse();
        }

        [Fact]
        public async Task Should_Stream_Progress_In_Real_Time_While_Installer_Is_Running()
        {
            await using var harness = await AgentHarness.StartAsync();
            var update = CreateUpdate("update-realtime");

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(new List<DriverUpdate> { update }));

            var installerBlocker = new TaskCompletionSource<bool>();

            harness.Installer
                .InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var progress = callInfo.ArgAt<IProgress<InstallProgress>>(1);
                    progress?.Report(new InstallProgress
                    {
                        PercentComplete = 15,
                        Phase = InstallPhase.Downloading,
                        StatusMessage = "Descarga en curso..."
                    });

                    await installerBlocker.Task;

                    return CreateInstallResult("update-realtime", success: true);
                });

            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "update-realtime" } });

            var hasFirstMessage = await call.ResponseStream.MoveNext(default);
            hasFirstMessage.Should().BeTrue();
            var firstMessage = call.ResponseStream.Current;
            firstMessage.Finished.Should().BeFalse();
            firstMessage.PercentComplete.Should().Be(15);
            firstMessage.Phase.Should().Be(InstallPhaseProto.InstallPhaseDownloading);

            installerBlocker.SetResult(true);

            var hasSecondMessage = await call.ResponseStream.MoveNext(default);
            hasSecondMessage.Should().BeTrue();
            var secondMessage = call.ResponseStream.Current;
            secondMessage.Finished.Should().BeTrue();
            secondMessage.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Should_Install_Several_Updates_Sequentially_And_Continue_After_Failures()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            harness.Installer
                .InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => CreateInstallResult(callInfo.ArgAt<DriverUpdate>(0).UpdateId, success: false));

            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "update-1", "update-2" } });
            var messages = await CollectAllAsync(call.ResponseStream);

            var finished = messages.Where(m => m.Finished).ToList();
            finished.Should().HaveCount(2);
            finished.Select(m => m.UpdateId).Should().Equal("update-1", "update-2");
            finished[0].Success.Should().BeFalse();
            finished[1].Success.Should().BeFalse();

            await harness.Installer.Received(2).InstallAsync(
                Arg.Any<DriverUpdate>(),
                Arg.Any<IProgress<InstallProgress>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Continue_Batch_When_An_Update_Throws_Unexpected_Exception()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            harness.Installer
                .InstallAsync(
                    Arg.Is<DriverUpdate>(u => u.UpdateId == "update-1"),
                    Arg.Any<IProgress<InstallProgress>>(),
                    Arg.Any<CancellationToken>())
                .Returns<Task<InstallResult>>(_ => throw new InvalidOperationException("Fallo catastrofico COM simulado"));

            harness.Installer
                .InstallAsync(
                    Arg.Is<DriverUpdate>(u => u.UpdateId == "update-2"),
                    Arg.Any<IProgress<InstallProgress>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => CreateInstallResult("update-2", success: true));

            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "update-1", "update-2" } });
            var messages = await CollectAllAsync(call.ResponseStream);

            var finished = messages.Where(m => m.Finished).ToList();
            finished.Should().HaveCount(2);

            finished[0].UpdateId.Should().Be("update-1");
            finished[0].Success.Should().BeFalse();
            finished[0].ErrorMessage.Should().Contain("Fallo catastrofico COM simulado");
            finished[0].CompletedUpdates.Should().Be(1);

            finished[1].UpdateId.Should().Be("update-2");
            finished[1].Success.Should().BeTrue();
            finished[1].CompletedUpdates.Should().Be(2);

            // Regla 8 de AGENTS.md: Registro obligatorio en el historial ante excepciones
            await harness.History.Received(1).RecordAsync(
                Arg.Is<OperationLogEntry>(e =>
                    e.UpdateId == "update-1" &&
                    !e.Success &&
                    e.ErrorMessage != null &&
                    e.ErrorMessage.Contains("Fallo catastrofico COM simulado")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Stop_Batch_Without_Retrying_When_Client_Disconnects()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            harness.Installer
                .InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => CreateInstallResult(callInfo.ArgAt<DriverUpdate>(0).UpdateId, success: true));

            using var cts = new CancellationTokenSource();
            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "update-1", "update-2" } }, cancellationToken: cts.Token);

            cts.Cancel();

            var act = async () =>
            {
                await foreach (var _ in call.ResponseStream.ReadAllAsync())
                {
                }
            };

            await act.Should().ThrowAsync<RpcException>();

            var status = await PollUntilAsync(
                async () => await harness.Client.GetStatusAsync(new StatusRequest()),
                s => !s.OperationInProgress && s.QueuedOperations == 0,
                TimeSpan.FromSeconds(10));
            status.OperationInProgress.Should().BeFalse();
        }

        [Fact]
        public async Task Should_Break_Batch_Without_Duplicate_History_When_Progress_Stream_Write_Throws_IOException()
        {
            var inventory = Substitute.For<IDriverInventory>();
            var updateSource = Substitute.For<IUpdateSource>();
            var installer = Substitute.For<IDriverInstaller>();
            var history = Substitute.For<IOperationHistory>();
            var queue = new OperationQueue();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverAgentGrpcService>.Instance;

            var service = new DriverAgentGrpcService(inventory, updateSource, installer, history, queue, logger);

            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };
            inventory.GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            updateSource.FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            // El instalador reporta progreso y finaliza con éxito
            installer.InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var prog = callInfo.ArgAt<IProgress<InstallProgress>>(1);
                    prog.Report(new InstallProgress { Phase = InstallPhase.Downloading, PercentComplete = 50, StatusMessage = "Descargando..." });
                    return Task.FromResult(CreateInstallResult(callInfo.ArgAt<DriverUpdate>(0).UpdateId, success: true));
                });

            var streamWriter = Substitute.For<IServerStreamWriter<InstallProgressMsg>>();
            // La llamada de escritura al stream falla durante un mensaje de PROGRESO (no de finalización)
            streamWriter.WriteAsync(Arg.Is<InstallProgressMsg>(m => !m.Finished), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new IOException("Named pipe roto durante progreso"));

            var context = new TestServerCallContext();

            await service.Install(new InstallRequest { UpdateIds = { "update-1", "update-2" } }, streamWriter, context);

            // El lote se interrumpe: update-1 se instaló pero update-2 no se llega a iniciar
            await installer.Received(1).InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>());
            await installer.DidNotReceive().InstallAsync(
                Arg.Is<DriverUpdate>(u => u.UpdateId == "update-2"),
                Arg.Any<IProgress<InstallProgress>>(),
                Arg.Any<CancellationToken>());

            // Aserción estricta: el servicio no debe llamar a RecordAsync bajo ningún argumento,
            // evitando duplicar o registrar falsos fallos en el historial si el instalador tuvo éxito
            await history.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        }

        [Fact]
        public async Task Should_Break_Batch_Without_Extra_Failure_When_Finished_Stream_Write_Throws_IOException()
        {
            var inventory = Substitute.For<IDriverInventory>();
            var updateSource = Substitute.For<IUpdateSource>();
            var installer = Substitute.For<IDriverInstaller>();
            var history = Substitute.For<IOperationHistory>();
            var queue = new OperationQueue();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverAgentGrpcService>.Instance;

            var service = new DriverAgentGrpcService(inventory, updateSource, installer, history, queue, logger);

            var updates = new List<DriverUpdate> { CreateUpdate("update-1"), CreateUpdate("update-2") };
            inventory.GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            updateSource.FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            installer.InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => CreateInstallResult(callInfo.ArgAt<DriverUpdate>(0).UpdateId, success: true));

            var streamWriter = Substitute.For<IServerStreamWriter<InstallProgressMsg>>();
            // La llamada de escritura al stream falla al enviar el Finished
            streamWriter.WriteAsync(Arg.Is<InstallProgressMsg>(m => m.Finished), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new IOException("Named pipe roto al escribir finished"));

            var context = new TestServerCallContext();

            await service.Install(new InstallRequest { UpdateIds = { "update-1", "update-2" } }, streamWriter, context);

            // El lote se interrumpe tras el fallo de transporte: update-2 nunca se procesa
            await installer.Received(1).InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>());
            await installer.DidNotReceive().InstallAsync(
                Arg.Is<DriverUpdate>(u => u.UpdateId == "update-2"),
                Arg.Any<IProgress<InstallProgress>>(),
                Arg.Any<CancellationToken>());

            // Aserción estricta: ningún registro extra en el historial
            await history.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        }

        [Fact]
        public async Task Should_Break_Batch_When_Stream_Write_Throws_IOException_On_NotFound_Update()
        {
            var inventory = Substitute.For<IDriverInventory>();
            var updateSource = Substitute.For<IUpdateSource>();
            var installer = Substitute.For<IDriverInstaller>();
            var history = Substitute.For<IOperationHistory>();
            var queue = new OperationQueue();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverAgentGrpcService>.Instance;

            var service = new DriverAgentGrpcService(inventory, updateSource, installer, history, queue, logger);

            inventory.GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            // Ninguna actualización disponible en Windows Update
            updateSource.FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(new List<DriverUpdate>()));

            var streamWriter = Substitute.For<IServerStreamWriter<InstallProgressMsg>>();
            // La escritura falla inmediatamente simulando desconexión en actualización no encontrada
            streamWriter.WriteAsync(Arg.Any<InstallProgressMsg>(), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new IOException("Named pipe roto en not found"));

            var context = new TestServerCallContext();

            // Debe capturar el error de transporte y no relanzar excepción no controlada
            var act = () => service.Install(new InstallRequest { UpdateIds = { "missing-1", "missing-2" } }, streamWriter, context);
            await act.Should().NotThrowAsync();

            // Solo se intentó escribir para "missing-1"; "missing-2" no se procesa porque el lote se cortó
            await streamWriter.Received(1).WriteAsync(
                Arg.Is<InstallProgressMsg>(m => m.UpdateId == "missing-1"),
                Arg.Any<CancellationToken>());
            await streamWriter.DidNotReceive().WriteAsync(
                Arg.Is<InstallProgressMsg>(m => m.UpdateId == "missing-2"),
                Arg.Any<CancellationToken>());

            await history.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        }

        [Fact]
        public async Task Should_Propagate_OperationCanceledException_When_Client_Cancels_During_Progress_Streaming()
        {
            var inventory = Substitute.For<IDriverInventory>();
            var updateSource = Substitute.For<IUpdateSource>();
            var installer = Substitute.For<IDriverInstaller>();
            var history = Substitute.For<IOperationHistory>();
            var queue = new OperationQueue();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverAgentGrpcService>.Instance;

            var service = new DriverAgentGrpcService(inventory, updateSource, installer, history, queue, logger);

            var updates = new List<DriverUpdate> { CreateUpdate("update-1") };
            inventory.GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            updateSource.FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            using var cts = new CancellationTokenSource();

            // Al escribir el progreso en el stream, el cliente cancela la RPC
            var streamWriter = Substitute.For<IServerStreamWriter<InstallProgressMsg>>();
            streamWriter.WriteAsync(Arg.Is<InstallProgressMsg>(m => !m.Finished), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                });

            installer.InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var prog = callInfo.ArgAt<IProgress<InstallProgress>>(1);
                    var token = callInfo.ArgAt<CancellationToken>(2);
                    prog.Report(new InstallProgress { Phase = InstallPhase.Downloading, PercentComplete = 20, StatusMessage = "Descargando..." });
                    // Esperar a que se propague la cancelación
                    await Task.Delay(50, token);
                    return CreateInstallResult("update-1", success: true);
                });

            var context = new TestServerCallContext(cts.Token);

            var act = () => service.Install(new InstallRequest { UpdateIds = { "update-1" } }, streamWriter, context);

            await act.Should().ThrowAsync<OperationCanceledException>();
            await history.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        }

        [Fact]
        public async Task Should_Not_Duplicate_History_Recording_When_Installer_Returns_Failed_Result()
        {
            var inventory = Substitute.For<IDriverInventory>();
            var updateSource = Substitute.For<IUpdateSource>();
            var installer = Substitute.For<IDriverInstaller>();
            var history = Substitute.For<IOperationHistory>();
            var queue = new OperationQueue();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverAgentGrpcService>.Instance;

            var service = new DriverAgentGrpcService(inventory, updateSource, installer, history, queue, logger);

            var updates = new List<DriverUpdate> { CreateUpdate("update-1") };
            inventory.GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            updateSource.FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            // El instalador gestiona internamente el error y devuelve Success = false
            installer.InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(CreateInstallResult("update-1", success: false));

            var streamWriter = Substitute.For<IServerStreamWriter<InstallProgressMsg>>();
            var context = new TestServerCallContext();

            await service.Install(new InstallRequest { UpdateIds = { "update-1" } }, streamWriter, context);

            // DriverAgentGrpcService no duplica el registro en el historial porque el instalador ya gestionó el resultado
            await history.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        }

        [Fact]
        public async Task Should_Report_NotFound_When_The_Update_Is_No_Longer_Available()
        {
            await using var harness = await AgentHarness.StartAsync();

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(new List<DriverUpdate>()));

            using var call = harness.Client.Install(new InstallRequest { UpdateIds = { "desconocida" } });
            var messages = await CollectAllAsync(call.ResponseStream);

            var finished = messages.Should().ContainSingle(m => m.Finished).Subject;
            finished.UpdateId.Should().Be("desconocida");
            finished.Success.Should().BeFalse();
            finished.StatusMessage.Should().Contain("desconocida");

            await harness.Installer.DidNotReceiveWithAnyArgs().InstallAsync(default!, default, default);
        }

        [Fact]
        public async Task Should_Reject_Empty_Requests()
        {
            await using var harness = await AgentHarness.StartAsync();

            using var call = harness.Client.Install(new InstallRequest());
            var act = () => CollectAllAsync(call.ResponseStream);

            await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == StatusCode.InvalidArgument);
        }

        [Fact]
        public async Task Should_Serialize_Concurrent_Operations()
        {
            await using var harness = await AgentHarness.StartAsync();
            var updates = new List<DriverUpdate> { CreateUpdate("update-A"), CreateUpdate("update-B") };

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(updates));

            using var blockFirst = new SemaphoreSlim(0, 1);
            using var firstEntered = new ManualResetEventSlim(false);
            using var secondEntered = new ManualResetEventSlim(false);

            harness.Installer
                .InstallAsync(
                    Arg.Is<DriverUpdate>(u => u.UpdateId == "update-A"),
                    Arg.Any<IProgress<InstallProgress>>(),
                    Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    firstEntered.Set();
                    callInfo.ArgAt<IProgress<InstallProgress>>(1)?.Report(
                        new InstallProgress { PercentComplete = 25, Phase = InstallPhase.Downloading, StatusMessage = "Descargando..." });
                    await blockFirst.WaitAsync(callInfo.ArgAt<CancellationToken>(2));
                    return CreateInstallResult("update-A", success: true);
                });

            harness.Installer
                .InstallAsync(
                    Arg.Is<DriverUpdate>(u => u.UpdateId == "update-B"),
                    Arg.Any<IProgress<InstallProgress>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    secondEntered.Set();
                    return CreateInstallResult("update-B", success: true);
                });

            var client = harness.Client;

            var firstCollect = Task.Run(async () =>
            {
                using var call = client.Install(new InstallRequest { UpdateIds = { "update-A" } });
                return await CollectAllAsync(call.ResponseStream);
            });

            // La primera operación debe quedar bloqueada dentro del instalador.
            firstEntered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

            var secondCollect = Task.Run(async () =>
            {
                using var call = client.Install(new InstallRequest { UpdateIds = { "update-B" } });
                return await CollectAllAsync(call.ResponseStream);
            });

            // Mientras la primera instalación está en curso, la segunda queda
            // en la cola y el estado refleja la operación activa con su progreso.
            var status = await PollUntilAsync(
                async () => await client.GetStatusAsync(new StatusRequest()),
                s => s.QueuedOperations == 2 && s.CurrentUpdateId == "update-A",
                TimeSpan.FromSeconds(10));
            status.OperationInProgress.Should().BeTrue();
            status.PercentComplete.Should().Be(25);
            status.Phase.Should().Be(InstallPhaseProto.InstallPhaseDownloading);
            secondEntered.IsSet.Should().BeFalse();

            blockFirst.Release();

            var firstMessages = await firstCollect;
            var secondMessages = await secondCollect;

            firstMessages.Should().ContainSingle(m => m.Finished && m.UpdateId == "update-A");
            secondMessages.Should().ContainSingle(m => m.Finished && m.UpdateId == "update-B");
            secondEntered.IsSet.Should().BeTrue();
        }

        [Fact]
        public async Task Should_Release_The_Queue_When_The_Client_Cancels_The_Install()
        {
            await using var harness = await AgentHarness.StartAsync();
            var update = CreateUpdate("update-1");

            harness.Inventory
                .GetInstalledDriversAsync(Arg.Any<CancellationToken>())
                .Returns(new List<InstalledDriver> { CreateInstalledDriver() });
            harness.UpdateSource
                .FindUpdatesAsync(Arg.Any<IReadOnlyList<InstalledDriver>>(), Arg.Any<CancellationToken>())
                .Returns(ToAsyncEnumerable(new List<DriverUpdate> { update }));

            using var installerStarted = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();

            harness.Installer
                .InstallAsync(Arg.Any<DriverUpdate>(), Arg.Any<IProgress<InstallProgress>>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    installerStarted.Set();
                    await Task.Delay(Timeout.InfiniteTimeSpan, callInfo.ArgAt<CancellationToken>(2));
                    return CreateInstallResult("update-1", success: true);
                });

            var client = harness.Client;
            using var call = client.Install(new InstallRequest { UpdateIds = { "update-1" } }, cancellationToken: cts.Token);
            var consume = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in call.ResponseStream.ReadAllAsync())
                    {
                    }
                }
                catch (RpcException)
                {
                    // Cancelación esperada por el lado cliente.
                }
            });

            installerStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

            cts.Cancel();
            await consume;

            var status = await PollUntilAsync(
                async () => await client.GetStatusAsync(new StatusRequest()),
                s => !s.OperationInProgress && s.QueuedOperations == 0,
                TimeSpan.FromSeconds(10));

            status.OperationInProgress.Should().BeFalse();
            status.QueuedOperations.Should().Be(0);
        }
    }

    public class Rollback
    {
        [Fact]
        public async Task Should_Pass_The_Operation_Id_And_Map_The_Result()
        {
            await using var harness = await AgentHarness.StartAsync();

            harness.Installer
                .RollbackAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(CreateInstallResult("update-1", success: true));

            var result = await harness.Client.RollbackAsync(new RollbackRequest { OperationId = "42" });

            result.Success.Should().BeTrue();
            result.InstalledVersion.Should().Be("2.0.0.0");

            await harness.Installer.Received(1).RollbackAsync(Arg.Is("42"), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("-1")]
        [InlineData("-42")]
        [InlineData("+10")]
        [InlineData("0")]
        public async Task Should_Reject_Invalid_Operation_Ids(string operationId)
        {
            await using var harness = await AgentHarness.StartAsync();

            var act = async () => await harness.Client.RollbackAsync(new RollbackRequest { OperationId = operationId });

            await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == StatusCode.InvalidArgument);
            await harness.Installer.DidNotReceiveWithAnyArgs().RollbackAsync(default!, default);
        }
    }

    public class GetHistory
    {
        [Fact]
        public async Task Should_Return_All_Entries_When_No_Limit_Is_Set()
        {
            await using var harness = await AgentHarness.StartAsync();
            harness.History
                .GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(Enumerable.Range(1, 3).Select(CreateHistoryEntry).ToList());

            var response = await harness.Client.GetHistoryAsync(new HistoryRequest());

            response.Entries.Should().HaveCount(3);
            response.Entries.Select(e => e.Id).Should().Equal(1L, 2L, 3L);
        }

        [Fact]
        public async Task Should_Return_Only_The_Most_Recent_Entries_When_Limited()
        {
            await using var harness = await AgentHarness.StartAsync();
            harness.History
                .GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(Enumerable.Range(1, 5).Select(CreateHistoryEntry).ToList());

            var response = await harness.Client.GetHistoryAsync(new HistoryRequest { Limit = 2 });

            response.Entries.Should().HaveCount(2);
            response.Entries.Select(e => e.Id).Should().Equal(4L, 5L);
        }

        private static OperationLogEntry CreateHistoryEntry(int id) => new()
        {
            Id = id,
            OperationType = "Install",
            UpdateId = $"update-{id}",
            DeviceName = "Dispositivo",
            HardwareId = "PCI\\VEN_0001",
            DriverTitle = $"Actualización {id}",
            PreviousVersion = "1.0.0.0",
            NewVersion = "2.0.0.0",
            Success = true,
            ErrorMessage = null,
            Timestamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    public class PipeAcl
    {
        [Fact]
        public async Task Configured_PipeSecurity_Should_Reach_The_Transport_And_Allow_Local_Clients()
        {
            var pipeName = $"driverupdater-tests-{Guid.NewGuid():N}";
            PipeSecurity? securitySeenByTransport = null;

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrelOptions =>
            {
                kestrelOptions.ListenNamedPipe(pipeName, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();
            builder.Services.AddSingleton(Substitute.For<IDriverInventory>());
            builder.Services.AddSingleton(Substitute.For<IUpdateSource>());
            builder.Services.AddSingleton(Substitute.For<IDriverInstaller>());
            builder.Services.AddSingleton(Substitute.For<IOperationHistory>());
            builder.Services.AddSingleton<IOperationQueue, OperationQueue>();
            var pipeStreamFactory = new NamedPipeStreamFactory();
            builder.Services.Configure<NamedPipeTransportOptions>(options =>
            {
                options.CurrentUserOnly = false;

                var security = new PipeSecurity();
                security.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User!,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                options.PipeSecurity = security;

                // Interceptar la creación de instancias del pipe para comprobar
                // que la ACL configurada es exactamente la que usa el transporte.
                options.CreateNamedPipeServerStream = context =>
                {
                    securitySeenByTransport = context.PipeSecurity;
                    return pipeStreamFactory.Create(pipeName, context);
                };
            });

            var app = builder.Build();
            app.MapGrpcService<DriverAgentGrpcService>();
            await app.StartAsync();

            try
            {
                using var channel = DriverAgentChannel.CreateChannel(pipeName);
                var client = new DriverAgent.DriverAgentClient(channel);

                var response = await client.GetStatusAsync(new StatusRequest(), deadline: DateTime.UtcNow.AddSeconds(10));
                response.Should().NotBeNull();

                securitySeenByTransport.Should().NotBeNull();
                var rules = securitySeenByTransport!
                    .GetAccessRules(true, true, typeof(SecurityIdentifier))
                    .Cast<PipeAccessRule>()
                    .ToList();
                rules.Should().ContainSingle(rule =>
                    ((SecurityIdentifier)rule.IdentityReference).Value == WindowsIdentity.GetCurrent().User!.Value
                    && rule.PipeAccessRights == PipeAccessRights.FullControl
                    && rule.AccessControlType == AccessControlType.Allow);
            }
            finally
            {
                await app.DisposeAsync();
            }
        }
    }

    private static async Task<StatusResponse> PollUntilAsync(
        Func<Task<StatusResponse>> get,
        Func<StatusResponse, bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        StatusResponse last = new();
        while (DateTime.UtcNow < deadline)
        {
            last = await get();
            if (condition(last))
            {
                return last;
            }

            await Task.Delay(50);
        }

        return last;
    }
}
