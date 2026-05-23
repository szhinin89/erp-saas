using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta XadesBesSigner a la interfaz inyectable IElectronicDocumentSigner.
/// Registrado como Singleton — XadesBesSigner no tiene estado de instancia.
/// </summary>
public sealed class ElectronicDocumentSignerAdapter : IElectronicDocumentSigner
{
    private readonly XadesBesSigner _signer = new();

    public byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password)
        => _signer.Sign(xmlUtf8, p12FilePath, p12Password);
}
