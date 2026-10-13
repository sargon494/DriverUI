namespace Agent.Drivers.Install;

/// <summary>
/// Resultado del proceso de descarga de una actualización en Windows Update Agent.
/// </summary>
internal sealed record WuaDownloadResult
{
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
    public IReadOnlyList<string> DownloadedFilePaths { get; init; } = [];
}

/// <summary>
/// Resultado del proceso de instalación de una actualización en Windows Update Agent.
/// </summary>
internal sealed record WuaInstallExecutionResult
{
    public required bool Success { get; init; }
    public required bool RebootRequired { get; init; }
    public required string? ErrorMessage { get; init; }
}

/// <summary>
/// Abstracción interna sobre las operaciones de sesión de Windows Update Agent (COM).
/// Permite aislar y simular la interacción con la API COM en pruebas unitarias.
/// </summary>
internal interface IWuaExecutor
{
    Task<WuaDownloadResult> DownloadAsync(string updateId, CancellationToken ct = default);
    Task<WuaInstallExecutionResult> InstallAsync(string updateId, CancellationToken ct = default);
}
