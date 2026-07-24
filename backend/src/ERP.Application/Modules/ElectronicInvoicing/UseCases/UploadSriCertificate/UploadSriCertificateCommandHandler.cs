using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UploadSriCertificate;

public sealed class UploadSriCertificateCommandHandler
    : IRequestHandler<UploadSriCertificateCommand, Result<SriCertificateUploadResultDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretProtector _secretProtector;
    private readonly ISriCertificateInspector _certInspector;
    private readonly IFileStorage _fileStorage;

    public UploadSriCertificateCommandHandler(
        ISriSettingsRepository repo,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ISecretProtector secretProtector,
        ISriCertificateInspector certInspector,
        IFileStorage fileStorage)
    {
        _repo            = repo;
        _currentCompany  = currentCompany;
        _currentUser     = currentUser;
        _secretProtector = secretProtector;
        _certInspector   = certInspector;
        _fileStorage     = fileStorage;
    }

    public async Task<Result<SriCertificateUploadResultDto>> Handle(
        UploadSriCertificateCommand request, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;

        var settings = await _repo.GetByCompanyIdAsync(companyId, cancellationToken);
        if (settings is null)
        {
            return Result<SriCertificateUploadResultDto>.ValidationFailure(
                "Debe guardar el ambiente y la URL del webservice SRI antes de cargar el certificado.");
        }

        using var buffer = new MemoryStream();
        await request.File.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        // Chequeo estructural liviano (ASN.1 DER SEQUENCE) — un PKCS#12 real siempre
        // empieza con 0x30. No reemplaza la validación completa, que requiere la
        // contraseña (se hace en InspectSriCertificateQuery / al validar contraseña).
        if (bytes.Length < 4 || bytes[0] != 0x30)
        {
            return Result<SriCertificateUploadResultDto>.ValidationFailure(
                "El archivo no tiene una estructura PKCS#12 (.p12/.pfx) válida.");
        }

        var storedPath = $"certificates/{companyId:N}/certificate.p12";
        buffer.Position = 0;
        await _fileStorage.SaveAsync(storedPath, buffer, cancellationToken);

        var uploadedAtUtc = DateTime.UtcNow;
        settings.AttachCertificate(storedPath, request.File.FileName, request.File.SizeBytes, uploadedAtUtc, _currentUser.UserId);
        await _repo.UpdateAsync(settings, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        SriCertificateInfoDto? inspection = null;
        if (!string.IsNullOrWhiteSpace(settings.CertPassword))
        {
            var password = _secretProtector.UnprotectOrPlaintext(settings.CertPassword);
            var result = _certInspector.Inspect(bytes, password);
            int? daysRemaining = result.NotAfterUtc.HasValue
                ? (int)Math.Ceiling((result.NotAfterUtc.Value - DateTime.UtcNow).TotalDays)
                : null;
            inspection = new SriCertificateInfoDto(
                result.PasswordCorrect, result.Loaded, result.NotAfterUtc, daysRemaining,
                result.Subject, result.Issuer, result.ErrorMessage);
        }

        return Result<SriCertificateUploadResultDto>.Success(new SriCertificateUploadResultDto(
            request.File.FileName, request.File.SizeBytes, uploadedAtUtc, inspection));
    }
}
