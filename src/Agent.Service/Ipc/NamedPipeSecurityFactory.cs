using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Agent.Service.Ipc;

/// <summary>
/// Construye la ACL del named pipe del servicio (regla de seguridad 4 de
/// <c>AGENTS.md</c>): el acceso queda restringido a SYSTEM, administradores
/// y usuarios interactivos locales. Se concede control completo a SYSTEM y
/// Administradores, y permisos de lectura/escritura (ReadWrite) a los usuarios
/// interactivos, evitando otorgar WRITE_DAC o WRITE_OWNER para que un usuario
/// sin privilegios no pueda alterar la seguridad del pipe. La frontera de
/// seguridad es "solo sesiones interactivas locales".
/// </summary>
internal static class NamedPipeSecurityFactory
{
    /// <summary>SID de la cuenta SYSTEM.</summary>
    internal const string LocalSystemSid = "S-1-5-18";

    /// <summary>SID del grupo integrado de administradores.</summary>
    internal const string BuiltinAdministratorsSid = "S-1-5-32-544";

    /// <summary>SID del grupo de usuarios interactivos (sesión local).</summary>
    internal const string InteractiveSid = "S-1-5-4";

    /// <summary>
    /// Crea la política de seguridad por defecto del pipe de DriverUpdater.
    /// </summary>
    internal static PipeSecurity CreateDefault()
    {
        var security = new PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(LocalSystemSid),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(BuiltinAdministratorsSid),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(InteractiveSid),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return security;
    }
}
