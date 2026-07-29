using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;

public sealed record GetBranchInvoiceOrgSettingsQuery(Guid BranchId)
    : IRequest<Result<BranchInvoiceOrgSettingsDto>>,
        IBranchScopedRequest;
