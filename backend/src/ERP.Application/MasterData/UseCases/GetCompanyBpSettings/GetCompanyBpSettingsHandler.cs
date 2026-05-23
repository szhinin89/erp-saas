using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpSettings;

public sealed class GetCompanyBpSettingsHandler
    : IRequestHandler<GetCompanyBpSettingsQuery, Result<CompanyBpSettingsDto?>>
{
    private readonly ICompanyBpSettingsRepository _repo;
    private readonly ICurrentCompany _company;

    public GetCompanyBpSettingsHandler(ICompanyBpSettingsRepository repo, ICurrentCompany company)
    {
        _repo = repo;
        _company = company;
    }

    public async Task<Result<CompanyBpSettingsDto?>> Handle(GetCompanyBpSettingsQuery query, CancellationToken ct)
    {
        var companyId = _company.CompanyId;
        if (companyId == Guid.Empty)
            return Result<CompanyBpSettingsDto?>.Failure("Se requiere empresa activa en el contexto de sesión.");

        var settings = await _repo.GetAsync(companyId, query.BusinessPartnerId, ct);
        return Result<CompanyBpSettingsDto?>.Success(
            settings is null ? null : CompanyBpSettingsDto.From(settings));
    }
}
