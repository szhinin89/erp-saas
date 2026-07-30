using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Único consumidor de <see cref="IElectronicDocumentSigner"/> dentro de ElectronicDocuments.
/// No reescribe XAdES ni duplica lógica criptográfica — solo resuelve la configuración SRI de
/// la empresa (<see cref="ISriSettingsRepository"/>), valida completitud/accesibilidad y
/// delega la firma en el motor ya existente.
/// </summary>
public sealed partial class ElectronicDocumentSigningService : IElectronicDocumentSigningService
{
    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IElectronicDocumentSigner _signer;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ElectronicDocumentSigningService> _logger;

    public ElectronicDocumentSigningService(
        ISriSettingsRepository sriSettingsRepository,
        ISecretProtector secretProtector,
        IElectronicDocumentSigner signer,
        IFileStorage fileStorage,
        ILogger<ElectronicDocumentSigningService> logger
    )
    {
        _sriSettingsRepository = sriSettingsRepository;
        _secretProtector = secretProtector;
        _signer = signer;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<Result<SignedElectronicDocumentXml>> SignAsync(
        Guid tenantId,
        Guid companyId,
        ElectronicDocumentXml xml,
        CancellationToken ct = default
    )
    {
        var sriSettings = await _sriSettingsRepository.GetByCompanyIdAsync(companyId, ct);
        if (sriSettings is null)
        {
            LogSigningPrerequisiteMissing(
                companyId,
                "La empresa no tiene configuración SRI registrada."
            );
            return Result<SignedElectronicDocumentXml>.ValidationFailure(
                "La empresa no tiene configuración SRI registrada — no se puede firmar el documento electrónico."
            );
        }

        if (string.IsNullOrWhiteSpace(sriSettings.CertP12Path))
        {
            LogSigningPrerequisiteMissing(companyId, "Sin certificado P12 configurado.");
            return Result<SignedElectronicDocumentXml>.ValidationFailure(
                "La empresa no tiene un certificado digital (P12) registrado en la configuración SRI."
            );
        }

        if (string.IsNullOrWhiteSpace(sriSettings.CertPassword))
        {
            LogSigningPrerequisiteMissing(companyId, "Sin contraseña de certificado configurada.");
            return Result<SignedElectronicDocumentXml>.ValidationFailure(
                "La empresa no tiene contraseña de certificado registrada en la configuración SRI."
            );
        }

        await using var certStream = await _fileStorage.GetAsync(sriSettings.CertP12Path, ct);
        if (certStream is null)
        {
            LogSigningPrerequisiteMissing(
                companyId,
                "El certificado P12 configurado no es accesible en el almacenamiento."
            );
            return Result<SignedElectronicDocumentXml>.NotFound(
                "El certificado digital configurado no es accesible en el almacenamiento."
            );
        }

        using var certBuffer = new MemoryStream();
        await certStream.CopyToAsync(certBuffer, ct);
        var certBytes = certBuffer.ToArray();

        string password;
        try
        {
            password = _secretProtector.UnprotectOrPlaintext(sriSettings.CertPassword);
        }
        catch (CryptographicException ex)
        {
            LogSigningFailed(companyId, "descifrado de contraseña", ex);
            return Result<SignedElectronicDocumentXml>.Failure(
                $"No se pudo descifrar la contraseña del certificado: {ex.Message}"
            );
        }

        byte[] signedBytes;
        try
        {
            signedBytes = _signer.Sign(xml.Xml, certBytes, password);
        }
        catch (CryptographicException ex)
        {
            LogSigningFailed(companyId, "criptográfico", ex);
            return Result<SignedElectronicDocumentXml>.Failure(
                $"Error criptográfico al firmar el documento electrónico: {ex.Message}"
            );
        }
        catch (InvalidOperationException ex)
        {
            // XadesBesSigner lanza esta excepción cuando el certificado no tiene clave privada
            // RSA utilizable — un problema del certificado, no de los datos del documento.
            LogSigningFailed(companyId, "certificado sin clave privada", ex);
            return Result<SignedElectronicDocumentXml>.Failure(
                $"Error de certificado al firmar el documento electrónico: {ex.Message}"
            );
        }
        catch (ArgumentException ex)
        {
            // X509CertificateLoader lanza esta excepción ante un P12/contraseña inválidos.
            LogSigningFailed(companyId, "P12/contraseña inválidos", ex);
            return Result<SignedElectronicDocumentXml>.Failure(
                $"Error de certificado al firmar el documento electrónico: {ex.Message}"
            );
        }
        catch (IOException ex)
        {
            LogSigningFailed(companyId, "lectura del certificado", ex);
            return Result<SignedElectronicDocumentXml>.NotFound(
                $"No se pudo leer el certificado digital: {ex.Message}"
            );
        }

        LogSigningSucceeded(companyId, xml.AccessKey);

        return Result<SignedElectronicDocumentXml>.Success(
            new SignedElectronicDocumentXml(
                SignedXml: Encoding.UTF8.GetString(signedBytes),
                Encoding: xml.Encoding,
                Version: xml.Version,
                DocumentType: xml.DocumentType,
                AccessKey: xml.AccessKey,
                SignedAtUtc: DateTime.UtcNow
            )
        );
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Firma no ejecutada para empresa {CompanyId}: {Reason}"
    )]
    private partial void LogSigningPrerequisiteMissing(Guid companyId, string reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[ElectronicDocuments] Falló la firma electrónica para empresa {CompanyId} ({Stage})"
    )]
    private partial void LogSigningFailed(Guid companyId, string stage, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[ElectronicDocuments] Documento firmado correctamente para empresa {CompanyId} — claveAcceso {AccessKey}"
    )]
    private partial void LogSigningSucceeded(Guid companyId, string accessKey);
}
