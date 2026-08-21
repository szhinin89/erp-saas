using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;

public sealed class GetCompanyOperationalReadinessQueryHandler
    : IRequestHandler<GetCompanyOperationalReadinessQuery, Result<CompanyOperationalReadinessDto>>
{
    private readonly ICompanyOperationalReadinessResolver _resolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetCompanyOperationalReadinessQueryHandler(
        ICompanyOperationalReadinessResolver resolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _resolver = resolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<CompanyOperationalReadinessDto>> Handle(
        GetCompanyOperationalReadinessQuery request,
        CancellationToken cancellationToken
    )
    {
        var readiness = await _resolver.GetAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            cancellationToken
        );

        return Result<CompanyOperationalReadinessDto>.Success(
            new CompanyOperationalReadinessDto(
                OverallStatus: readiness.OverallStatus.ToString(),
                CanSell: readiness.CanSell,
                CanIssueElectronicInvoices: readiness.CanIssueElectronicInvoices,
                CanUseInventory: readiness.CanUseInventory,
                CanUseCashRegister: readiness.CanUseCashRegister,
                Sections: readiness
                    .Sections.Select(s => new CompanyOperationalReadinessSectionDto(
                        Code: s.Code,
                        Status: s.Status.ToString(),
                        Items: s
                            .Items.Select(i => new CompanyOperationalReadinessItemDto(
                                Code: i.Code,
                                Status: i.Status.ToString(),
                                Severity: i.Severity.ToString(),
                                BlockingArea: i.BlockingArea?.ToString(),
                                ActionTarget: i.ActionTarget?.ToString()
                            ))
                            .ToList()
                    ))
                    .ToList()
            )
        );
    }
}
