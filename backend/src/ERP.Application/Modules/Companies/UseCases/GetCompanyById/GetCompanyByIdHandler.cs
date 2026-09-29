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
        // Empresa pedida por id: inexistente, de otro tenant, sin membership o no operativa son la
        // MISMA respuesta (NOT_FOUND, mismo texto) — no se revela qué empresas existen. Se decide
        // por código: solo UNAUTHORIZED se propaga tal cual.
        if (!access.IsSuccess)
            return access.Code == ApiResponseCodes.Common.Unauthorized
                ? Result<CompanyDetailDto>.Failure(access.Error!, access.Code)
                : Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        var company = await _companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null || company.TenantId != access.Value!.TenantId)
            return Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
    }
}
