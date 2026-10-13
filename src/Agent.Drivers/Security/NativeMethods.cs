using System.Runtime.InteropServices;

namespace Agent.Drivers.Security;

/// <summary>
/// Declaraciones P/Invoke para la API WinVerifyTrust de wintrust.dll.
/// Permite verificar la firma digital Authenticode y de catálogo de archivos binarios.
/// </summary>
/// <remarks>
/// Referencia: https://learn.microsoft.com/windows/win32/api/wintrust/nf-wintrust-winverifytrust
/// </remarks>
internal static class NativeMethods
{
    // ─── GUID de acción ───────────────────────────────────────────────

    /// <summary>
    /// WINTRUST_ACTION_GENERIC_VERIFY_V2: verificación genérica Authenticode v2.
    /// </summary>
    internal static readonly Guid WintrustActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    // ─── Constantes WinTrust Data ─────────────────────────────────────

    /// <summary>Sin interfaz de usuario durante la verificación.</summary>
    internal const uint WtdUiNone = 2;

    /// <summary>No comprobar revocación de certificados.</summary>
    internal const uint WtdRevokeNone = 0;

    /// <summary>Verificar un archivo (pFile).</summary>
    internal const uint WtdChoiceFile = 1;

    /// <summary>Acción: ignorar estado (no mantener handle de estado).</summary>
    internal const uint WtdStateActionIgnore = 0;

    /// <summary>Acción: verificar la firma.</summary>
    internal const uint WtdStateActionVerify = 1;

    /// <summary>Acción: liberar el handle de estado.</summary>
    internal const uint WtdStateActionClose = 2;

    // ─── Códigos de error de confianza (HRESULT) ──────────────────────

    /// <summary>El archivo no tiene firma digital (TRUST_E_NOSIGNATURE).</summary>
    internal const int TrustErrorNoSignature = unchecked((int)0x800B0100);

    /// <summary>La firma está marcada como no confiable (TRUST_E_EXPLICIT_DISTRUST).</summary>
    internal const int TrustErrorExplicitDistrust = unchecked((int)0x800B0111);

    /// <summary>El sujeto de la firma no es de confianza (TRUST_E_SUBJECT_NOT_TRUSTED).</summary>
    internal const int TrustErrorSubjectNotTrusted = unchecked((int)0x800B0004);

    /// <summary>La política de seguridad del administrador impide la verificación (CRYPT_E_SECURITY_SETTINGS).</summary>
    internal const int CryptErrorSecuritySettings = unchecked((int)0x80092026);

    /// <summary>El certificado ha expirado (CERT_E_EXPIRED).</summary>
    internal const int CertErrorExpired = unchecked((int)0x800B0101);

    /// <summary>El certificado ha sido revocado (CERT_E_REVOKED).</summary>
    internal const int CertErrorRevoked = unchecked((int)0x800B010C);

    /// <summary>La firma no coincide con el digest del archivo (TRUST_E_BAD_DIGEST).</summary>
    internal const int TrustErrorBadDigest = unchecked((int)0x80096010);

    // ─── Función nativa ───────────────────────────────────────────────

    /// <summary>
    /// Verifica la confianza de un archivo firmado digitalmente.
    /// Retorna 0 (S_OK) si la firma es válida y de confianza.
    /// </summary>
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    internal static extern int WinVerifyTrust(
        IntPtr hwnd,
        ref Guid pgActionID,
        ref WintrustData pWVTData);

    // ─── Estructuras ──────────────────────────────────────────────────

    /// <summary>
    /// Estructura principal que describe los datos de verificación para WinVerifyTrust.
    /// Corresponde a WINTRUST_DATA en la API nativa.
    /// </summary>
    /// <remarks>
    /// Layout secuencial con alineación natural (8 bytes en x64).
    /// El campo <see cref="UnionData"/> apunta a la estructura específica según <see cref="UnionChoice"/>
    /// (ej: WintrustFileInfo para WTD_CHOICE_FILE).
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WintrustData
    {
        public uint CbStruct;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr UnionData;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    /// <summary>
    /// Información del archivo a verificar. Corresponde a WINTRUST_FILE_INFO.
    /// </summary>
    /// <remarks>
    /// <see cref="FilePath"/> es un puntero a cadena Unicode (LPCWSTR) gestionado manualmente
    /// con <see cref="Marshal.StringToCoTaskMemUni"/> para evitar fugas de memoria.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WintrustFileInfo
    {
        public uint CbStruct;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }
}
