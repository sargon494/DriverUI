using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.Security;

/// <summary>
/// Implementación de <see cref="ISignatureVerifier"/> usando la API nativa WinVerifyTrust de Windows.
/// </summary>
public sealed class WinTrustSignatureVerifier : ISignatureVerifier
{
    private readonly ILogger<WinTrustSignatureVerifier> _logger;

    public WinTrustSignatureVerifier(ILogger<WinTrustSignatureVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<SignatureVerificationResult> VerifyAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(new SignatureVerificationResult
            {
                IsValid = false,
                SignerName = null,
                FailureReason = $"El archivo no existe: '{filePath}'",
                IsWhql = false
            });
        }

        return Task.Run(() => VerifyInternal(filePath), ct);
    }

    private SignatureVerificationResult VerifyInternal(string filePath)
    {
        var fileInfo = new NativeMethods.WintrustFileInfo
        {
            CbStruct = (uint)Marshal.SizeOf<NativeMethods.WintrustFileInfo>(),
            FilePath = Marshal.StringToCoTaskMemUni(filePath),
            FileHandle = IntPtr.Zero,
            KnownSubject = IntPtr.Zero
        };

        var pFileInfo = Marshal.AllocCoTaskMem(Marshal.SizeOf<NativeMethods.WintrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, pFileInfo, false);

        var actionId = NativeMethods.WintrustActionGenericVerifyV2;

        var trustData = new NativeMethods.WintrustData
        {
            CbStruct = (uint)Marshal.SizeOf<NativeMethods.WintrustData>(),
            PolicyCallbackData = IntPtr.Zero,
            SipClientData = IntPtr.Zero,
            UiChoice = NativeMethods.WtdUiNone,
            RevocationChecks = NativeMethods.WtdRevokeNone,
            UnionChoice = NativeMethods.WtdChoiceFile,
            UnionData = pFileInfo,
            StateAction = NativeMethods.WtdStateActionIgnore,
            StateData = IntPtr.Zero,
            UrlReference = IntPtr.Zero,
            ProvFlags = 0x00000040 | 0x00001000, // WTD_REVOCATION_CHECK_CHAIN | WTD_CACHE_ONLY_URL_RETRIEVAL
            UiContext = 0,
            SignatureSettings = IntPtr.Zero
        };

        int result;
        try
        {
            result = NativeMethods.WinVerifyTrust(IntPtr.Zero, ref actionId, ref trustData);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fileInfo.FilePath);
            Marshal.DestroyStructure<NativeMethods.WintrustFileInfo>(pFileInfo);
            Marshal.FreeCoTaskMem(pFileInfo);
        }

        string? signerName = null;
        bool isWhql = false;

        if (result == 0)
        {
            // Intentar extraer el certificado para conocer el firmante
            try
            {
#pragma warning disable SYSLIB0057 // Obsoleto en .NET 9+, pero es la API estándar de BCL para leer firmas Authenticode de archivos PE
                using var rawCert = X509Certificate.CreateFromSignedFile(filePath);
                using var cert = new X509Certificate2(rawCert);
                signerName = cert.Subject;
                var commonName = cert.GetNameInfo(X509NameType.SimpleName, false);
                if (!string.IsNullOrEmpty(commonName))
                {
                    signerName = commonName;
                }

                if (signerName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                    signerName.Contains("Windows Hardware", StringComparison.OrdinalIgnoreCase))
                {
                    isWhql = true;
                }
#pragma warning restore SYSLIB0057
            }
            catch
            {
                // Si no se puede extraer el certificado embebido (ej. firma por catálogo .cat),
                // la firma es válida de acuerdo a WinVerifyTrust.
                signerName = "Firma Authenticode verificada";
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Firma verificada para {FilePath}: Válida, Firmante: {Signer}", filePath, signerName);
            }

            return new SignatureVerificationResult
            {
                IsValid = true,
                SignerName = signerName,
                FailureReason = null,
                IsWhql = isWhql
            };
        }

        var failureReason = GetFailureReason(result);
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Fallo en verificación de firma para {FilePath}: {Reason} (0x{HResult:X8})", filePath, failureReason, result);
        }

        return new SignatureVerificationResult
        {
            IsValid = false,
            SignerName = null,
            FailureReason = failureReason,
            IsWhql = false
        };
    }

    private static string GetFailureReason(int hresult) => hresult switch
    {
        NativeMethods.TrustErrorNoSignature => "El archivo no tiene firma digital (TRUST_E_NOSIGNATURE).",
        NativeMethods.TrustErrorExplicitDistrust => "La firma está explícitamente marcada como no confiable (TRUST_E_EXPLICIT_DISTRUST).",
        NativeMethods.TrustErrorSubjectNotTrusted => "El sujeto de la firma no es de confianza (TRUST_E_SUBJECT_NOT_TRUSTED).",
        NativeMethods.CryptErrorSecuritySettings => "La configuración de seguridad del sistema impide la verificación (CRYPT_E_SECURITY_SETTINGS).",
        NativeMethods.CertErrorExpired => "El certificado de la firma ha expirado (CERT_E_EXPIRED).",
        NativeMethods.CertErrorRevoked => "El certificado de la firma ha sido revocado (CERT_E_REVOKED).",
        NativeMethods.TrustErrorBadDigest => "La firma no coincide con el contenido del archivo (TRUST_E_BAD_DIGEST).",
        _ => $"Error de verificación de confianza: 0x{hresult:X8}"
    };
}
