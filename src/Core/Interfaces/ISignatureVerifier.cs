using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Verificador de firma digital de paquetes y archivos binarios de controladores.
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Verifica la firma digital del archivo especificado usando WinVerifyTrust.
    /// </summary>
    Task<SignatureVerificationResult> VerifyAsync(string filePath, CancellationToken ct = default);
}
