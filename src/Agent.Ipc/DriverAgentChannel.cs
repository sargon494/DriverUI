using System.IO.Pipes;
using Grpc.Core;
using Grpc.Net.Client;

namespace Agent.Ipc;

/// <summary>
/// Fábrica de canales gRPC sobre named pipes de Windows para comunicar la UI
/// (u otros clientes locales) con el servicio DriverUpdater.
/// </summary>
public static class DriverAgentChannel
{
    /// <summary>
    /// Nombre del named pipe por defecto publicado por el servicio
    /// (equivalente a <c>\\.\pipe\DriverUpdater</c>).
    /// </summary>
    public const string DefaultPipeName = "DriverUpdater";

    /// <summary>
    /// Crea un canal gRPC conectado al named pipe del servicio DriverUpdater.
    /// El canal es de texto claro sobre HTTP/2; la seguridad la aporta la ACL
    /// del propio pipe (restringida a usuarios interactivos y SYSTEM).
    /// </summary>
    /// <param name="pipeName">Nombre del pipe; por defecto <see cref="DefaultPipeName"/>.</param>
    /// <returns>Canal gRPC listo para usar; el invocador debe liberarlo con <c>Dispose</c>.</returns>
    public static GrpcChannel CreateChannel(string pipeName = DefaultPipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                _ = context; // La dirección http://localhost es simbólica; el transporte real es el pipe.

                var pipeStream = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                try
                {
                    await pipeStream.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipeStream;
                }
                catch
                {
                    await pipeStream.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        };

        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = handler,
            Credentials = ChannelCredentials.Insecure
        });
    }
}
