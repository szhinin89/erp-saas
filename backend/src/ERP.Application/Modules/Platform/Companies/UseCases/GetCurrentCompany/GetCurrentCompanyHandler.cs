using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.GetCurrentCompany;

public sealed class GetCurrentCompanyHandler : IRequestHandler<GetCurrentCompanyQuery, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;

    public GetCurrentCompanyHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies)
    {
        _accessGuard = accessGuard;
        _companies = companies;
    }

    public async Task<Result<CompanyDetailDto>> Handle(GetCurrentCompanyQuery request, CancellationToken ct)
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(ct);
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var company = await _companies.GetByIdAsync(access.Value!.CompanyId, ct);
        if (company is null)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
    }
}
