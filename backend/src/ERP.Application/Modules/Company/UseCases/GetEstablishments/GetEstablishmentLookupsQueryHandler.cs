using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

public sealed class GetEstablishmentLookupsQueryHandler
    : IRequestHandler<GetEstablishmentLookupsQuery, Result<IReadOnlyList<EstablishmentLookupDto>>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;

    public GetEstablishmentLookupsQueryHandler(
        IEstablishmentRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany company)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _company = company;
    }

    public async Task<Result<IReadOnlyList<EstablishmentLookupDto>>> Handle(
        GetEstablishmentLookupsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetActiveByCompanyAsync(
            _currentTenant.TenantId,
            _company.CompanyId,
            cancellationToken);

        var dtos = items
            .Select(e => new EstablishmentLookupDto(e.Id, e.Code, e.Name))
            .ToList();

        return Result<IReadOnlyList<EstablishmentLookupDto>>.Success(dtos);
    }
}
