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
    private readonly ICurrentBranch _currentBranch;

    public GetCompanyOperationalReadinessQueryHandler(
        ICompanyOperationalReadinessResolver resolver,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentBranch currentBranch
    )
    {
        _resolver = resolver;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentBranch = currentBranch;
    }

    public async Task<Result<CompanyOperationalReadinessDto>> Handle(
        GetCompanyOperationalReadinessQuery request,
        CancellationToken cancellationToken
    )
    {
        var readiness = await _resolver.GetAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            _currentBranch.BranchId,
            cancellationToken
        );

        return Result<CompanyOperationalReadinessDto>.Success(
            new CompanyOperationalReadinessDto(
                BranchId: readiness.BranchId,
                OverallStatus: readiness.OverallStatus.ToString(),
                CanSell: readiness.CanSell,
                CanIssueElectronicInvoices: readiness.CanIssueElectronicInvoices,
                CanUseInventory: readiness.CanUseInventory,
                CanUseCashRegister: readiness.CanUseCashRegister,
                Sections: readiness
                    .Sections.Select(s => new CompanyOperationalReadinessSectionDto(
                        Code: s.Code,
                        Status: s.Status.ToString(),
                        Items: s.Items.Select(i => new CompanyOperationalReadinessItemDto(
                                Code: i.Code,
                                Status: i.Status.ToString(),
                                Severity: i.Severity.ToString(),
                                BlockingAreas: i
                                    .BlockingAreas.Select(a => a.ToString())
                                    .ToList(),
                                ActionTarget: i.ActionTarget?.ToString()
                            ))
                            .ToList()
                    ))
                    .ToList(),
                CashRegisters: readiness
                    .CashRegisters.Select(r => new CompanyOperationalReadinessCashRegisterDto(
                        CashRegisterId: r.CashRegisterId,
                        Code: r.Code,
                        Name: r.Name,
                        EmissionPointId: r.EmissionPointId,
                        EstablishmentCode: r.EstablishmentCode,
                        EmissionPointCode: r.EmissionPointCode,
                        EmissionType: r.EmissionType?.ToString(),
                        OperationalStatus: r.OperationalStatus.ToString(),
                        FiscalStatus: r.FiscalStatus.ToString(),
                        RequirementCodes: r.RequirementCodes
                    ))
                    .ToList()
            )
        );
    }
}
