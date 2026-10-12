namespace Core.Models;

/// <summary>
/// Resultado de la verificación criptográfica de firma digital de un archivo o paquete de controladores.
/// </summary>
public sealed record SignatureVerificationResult
{
    public required bool IsValid { get; init; }
    public required string? SignerName { get; init; }
    public required string? FailureReason { get; init; }
    public required bool IsWhql { get; init; }
}
