using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;

public sealed class GetCurrentCompanyHandler
    : IRequestHandler<GetCurrentCompanyQuery, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;

    public GetCurrentCompanyHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies)
    {
        _accessGuard = accessGuard;
        _companies = companies;
    }

    public async Task<Result<CompanyDetailDto>> Handle(
        GetCurrentCompanyQuery request,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(access.Value!.CompanyId, cancellationToken);
        if (company is null)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
    }
}
