using System.IO.Pipes;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;

namespace Agent.Service.Ipc;

/// <summary>
/// Fábrica de instancias del named pipe del servicio para el hook
/// <c>NamedPipeTransportOptions.CreateNamedPipeServerStream</c>.
/// Garantiza que la opción <see cref="PipeOptions.FirstPipeInstance"/> (protección
/// frente a suplantación del nombre del pipe) se aplica únicamente a la primera
/// instancia real, y que cada instancia se crea con la ACL configurada.
/// </summary>
internal sealed class NamedPipeStreamFactory
{
    private int _firstInstanceCreated;

    /// <summary>
    /// Normaliza las opciones del contexto: la primera instancia conserva
    /// <see cref="PipeOptions.FirstPipeInstance"/>; las siguientes lo eliminan
    /// (el transporte lo marca en varias instancias del pool y fallarían con
    /// <c>Access Denied</c> al existir ya una instancia del mismo nombre).
    /// </summary>
    internal static PipeOptions NormalizePipeOptions(bool isFirstInstance, PipeOptions options)
    {
        if (isFirstInstance)
        {
            return options;
        }

        return options & ~PipeOptions.FirstPipeInstance;
    }

    /// <summary>
    /// Crea una instancia del pipe con las opciones y la ACL del contexto del transporte.
    /// </summary>
    public NamedPipeServerStream Create(string pipeName, CreateNamedPipeServerStreamContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(context);

        var isFirstInstance = Interlocked.Exchange(ref _firstInstanceCreated, 1) == 0;
        var pipeOptions = NormalizePipeOptions(isFirstInstance, context.PipeOptions);

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            pipeOptions,
            0,
            0,
            context.PipeSecurity);
    }
}
