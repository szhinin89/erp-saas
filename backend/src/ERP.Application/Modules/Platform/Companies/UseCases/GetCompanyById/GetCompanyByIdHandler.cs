using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.GetCompanyById;

public sealed class GetCompanyByIdHandler : IRequestHandler<GetCompanyByIdQuery, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;

    public GetCompanyByIdHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies)
    {
        _accessGuard = accessGuard;
        _companies = companies;
    }

    public async Task<Result<CompanyDetailDto>> Handle(GetCompanyByIdQuery request, CancellationToken ct)
    {
        var access = await _accessGuard.RequireMembershipAsync(request.Id, requireActiveCompany: false, ct);
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(request.Id, ct);
        if (company is null || company.SubscriberId != access.Value!.SubscriberId)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
    }
}
