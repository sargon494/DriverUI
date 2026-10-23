using System.Globalization;
using System.IO.Pipes;
using Agent.Drivers.DependencyInjection;
using Agent.Ipc;
using Agent.Service.Grpc;
using Agent.Service.Ipc;
using Agent.Service.Operations;
using Agent.Service.Scheduling;
using Agent.Service.Settings;
using Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(GetLogFilePath(), rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Permite ejecutar el binario como servicio de Windows ("DriverUpdater")
    // o directamente en consola con fines de diagnóstico.
    builder.Services.AddWindowsService(options => options.ServiceName = "DriverUpdater");

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    // Regla de seguridad 4 de AGENTS.md: el pipe solo acepta conexiones locales
    // de SYSTEM, administradores y usuarios interactivos.
    var pipeStreamFactory = new NamedPipeStreamFactory();
    builder.Services.Configure<NamedPipeTransportOptions>(options =>
    {
        // El transporte añade PipeOptions.CurrentUserOnly por defecto, que es
        // incompatible con una ACL explícita y además bloquearía a los usuarios
        // interactivos (el servicio corre como SYSTEM). La restricción de acceso
        // se aplica mediante la ACL del pipe.
        options.CurrentUserOnly = false;
        options.PipeSecurity = NamedPipeSecurityFactory.CreateDefault();

        // El creador por defecto del transporte marca varias instancias del pool
        // con PipeOptions.FirstPipeInstance y falla con Access Denied cuando ya
        // existe una instancia previa del mismo nombre. Se usa el punto de
        // extensión oficial creando cada instancia con la ACL del contexto y la
        // flag de primera instancia normalizada.
        options.CreateNamedPipeServerStream = context => pipeStreamFactory.Create(
            DriverAgentChannel.DefaultPipeName,
            context);
    });

    builder.WebHost.ConfigureKestrel(kestrelOptions =>
    {
        kestrelOptions.ListenNamedPipe(DriverAgentChannel.DefaultPipeName, listenOptions =>
        {
            // El canal IPC es gRPC puro; no hay HTTP/1.1 en el named pipe.
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    });

    builder.Services.AddGrpc();
    builder.Services.AddDriverServices();
    builder.Services.AddSingleton<IOperationQueue, OperationQueue>();
    builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
    builder.Services.AddSingleton<ScheduledCheckState>();
    builder.Services.AddHostedService<ScheduledCheckService>();

    var app = builder.Build();
    app.MapGrpcService<DriverAgentGrpcService>();

    Log.Information("Servicio DriverUpdater iniciado (pipe: {PipeName})", DriverAgentChannel.DefaultPipeName);

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio DriverUpdater terminó de forma inesperada.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static string GetLogFilePath()
{
    var logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DriverUpdater",
        "logs");
    return Path.Combine(logDirectory, "service-.log");
}
