using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;

public sealed record GetBranchInvoiceOrgSettingsQuery(Guid BranchId)
    : IRequest<Result<BranchInvoiceOrgSettingsDto>>, IBranchScopedRequest;
