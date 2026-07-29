using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyById;

public sealed class GetCompanyByIdHandler
    : IRequestHandler<GetCompanyByIdQuery, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;

    public GetCompanyByIdHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies)
    {
        _accessGuard = accessGuard;
        _companies = companies;
    }

    public async Task<Result<CompanyDetailDto>> Handle(
        GetCompanyByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireMembershipAsync(
            request.Id,
            requireActiveCompany: false,
            cancellationToken
        );
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null || company.TenantId != access.Value!.TenantId)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
    }
}
