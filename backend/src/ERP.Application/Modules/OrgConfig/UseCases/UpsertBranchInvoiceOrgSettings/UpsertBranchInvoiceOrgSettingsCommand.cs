using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;

public sealed record UpsertBranchInvoiceOrgSettingsCommand(Guid BranchId, Guid? DefaultWarehouseId)
    : IRequest<Result<BranchInvoiceOrgSettingsDto>>,
        IBranchScopedRequest;
