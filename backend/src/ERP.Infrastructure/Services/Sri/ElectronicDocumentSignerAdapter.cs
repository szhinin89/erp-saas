using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta XadesBesSigner a la interfaz inyectable IElectronicDocumentSigner.
/// Registrado como Singleton — XadesBesSigner no tiene estado de instancia.
/// </summary>
public sealed class ElectronicDocumentSignerAdapter : IElectronicDocumentSigner
{
    public byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password)
        => XadesBesSigner.Sign(xmlUtf8, p12FilePath, p12Password);

    public byte[] Sign(string xmlUtf8, byte[] p12Bytes, string p12Password)
        => XadesBesSigner.Sign(xmlUtf8, p12Bytes, p12Password);
}
