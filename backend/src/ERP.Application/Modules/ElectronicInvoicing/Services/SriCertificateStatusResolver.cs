using System.Security.Cryptography;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Modules.ElectronicInvoicing.Services;

public sealed class SriCertificateStatusResolver : ISriCertificateStatusResolver
{
    private readonly IFileStorage _fileStorage;
    private readonly ISecretProtector _secretProtector;
    private readonly ISriCertificateInspector _certInspector;

    public SriCertificateStatusResolver(
        IFileStorage fileStorage,
        ISecretProtector secretProtector,
        ISriCertificateInspector certInspector)
    {
        _fileStorage = fileStorage;
        _secretProtector = secretProtector;
        _certInspector = certInspector;
    }

    public async Task<SriCertificateStatus> ResolveAsync(SriSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.CertP12Path))
            return new SriCertificateStatus(false, false, false, null, null, null, "No hay ningún certificado cargado.");

        await using var stream = await _fileStorage.GetAsync(settings.CertP12Path, cancellationToken);
        if (stream is null)
            return new SriCertificateStatus(false, false, false, null, null, null, "El certificado configurado no es accesible en el almacenamiento.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.CertPassword))
            return new SriCertificateStatus(true, false, false, null, null, null, "No hay contraseña configurada para el certificado.");

        string password;
        try
        {
            password = _secretProtector.UnprotectOrPlaintext(settings.CertPassword);
        }
        catch (CryptographicException ex)
        {
            return new SriCertificateStatus(true, false, false, null, null, null, $"No se pudo descifrar la contraseña del certificado: {ex.Message}");
        }

        var inspection = _certInspector.Inspect(buffer.ToArray(), password);

        var notExpired = inspection.Loaded && inspection.NotAfterUtc.HasValue && inspection.NotAfterUtc.Value > DateTime.UtcNow;
        var valid = inspection.PasswordCorrect && inspection.Loaded && notExpired;

        return new SriCertificateStatus(
            Installed: true,
            PasswordCorrect: inspection.PasswordCorrect,
            Valid: valid,
            NotAfterUtc: inspection.NotAfterUtc,
            Subject: inspection.Subject,
            Issuer: inspection.Issuer,
            ErrorMessage: inspection.ErrorMessage);
    }
}
