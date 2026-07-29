using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.InspectSriCertificate;

public sealed class InspectSriCertificateQueryHandler
    : IRequestHandler<InspectSriCertificateQuery, Result<SriCertificateInfoDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISriCertificateInspector _certInspector;
    private readonly IFileStorage _fileStorage;

    public InspectSriCertificateQueryHandler(
        ISriSettingsRepository repo,
        ICurrentCompany currentCompany,
        ISriCertificateInspector certInspector,
        IFileStorage fileStorage)
    {
        _repo = repo;
        _currentCompany = currentCompany;
        _certInspector = certInspector;
        _fileStorage = fileStorage;
    }

    public async Task<Result<SriCertificateInfoDto>> Handle(
        InspectSriCertificateQuery request, CancellationToken cancellationToken)
    {
        var settings = await _repo.GetByCompanyIdAsync(_currentCompany.CompanyId, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.CertP12Path))
        {
            return Result<SriCertificateInfoDto>.NotFound(
                "No hay ningún certificado cargado todavía para esta empresa.");
        }

        await using var stream = await _fileStorage.GetAsync(settings.CertP12Path, cancellationToken);
        if (stream is null)
        {
            return Result<SriCertificateInfoDto>.NotFound(
                "El certificado configurado no es accesible en el almacenamiento.");
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var result = _certInspector.Inspect(buffer.ToArray(), request.Password);
        int? daysRemaining = result.NotAfterUtc.HasValue
            ? (int)Math.Ceiling((result.NotAfterUtc.Value - DateTime.UtcNow).TotalDays)
            : null;

        return Result<SriCertificateInfoDto>.Success(new SriCertificateInfoDto(
            result.PasswordCorrect, result.Loaded, result.NotAfterUtc, daysRemaining,
            result.Subject, result.Issuer, result.ErrorMessage));
    }
}
