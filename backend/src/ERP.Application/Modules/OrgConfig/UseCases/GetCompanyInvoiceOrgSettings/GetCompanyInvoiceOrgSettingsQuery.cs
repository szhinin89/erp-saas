using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;

public sealed record GetCompanyInvoiceOrgSettingsQuery
    : IRequest<Result<CompanyInvoiceOrgSettingsDto>>;
