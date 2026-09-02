using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;

public sealed class UpsertBranchInvoiceOrgSettingsCommandHandler
    : IRequestHandler<UpsertBranchInvoiceOrgSettingsCommand, Result<BranchInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly IBranchRepository _branchRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public UpsertBranchInvoiceOrgSettingsCommandHandler(
        IOrgSettingsRepository repo,
        IBranchRepository branchRepo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _branchRepo = branchRepo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<BranchInvoiceOrgSettingsDto>> Handle(
        UpsertBranchInvoiceOrgSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        var branch = await _branchRepo.GetByIdForCompanyAsync(
            tenantId,
            companyId,
            command.BranchId,
            cancellationToken
        );
        if (branch is null)
            return Result<BranchInvoiceOrgSettingsDto>.Failure(
                "La sucursal no existe o no pertenece a esta empresa."
            );

        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Branch,
            command.BranchId,
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            command.DefaultWarehouseId?.ToString(),
            SettingDataType.Guid,
            userId
        );

        await _repo.UpsertAsync(setting, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<BranchInvoiceOrgSettingsDto>.Success(
            new BranchInvoiceOrgSettingsDto(command.DefaultWarehouseId)
        );
    }
}
