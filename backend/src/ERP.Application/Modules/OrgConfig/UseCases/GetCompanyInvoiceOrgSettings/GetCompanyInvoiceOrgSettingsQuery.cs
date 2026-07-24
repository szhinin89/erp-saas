using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;

public sealed record GetCompanyInvoiceOrgSettingsQuery
    : IRequest<Result<CompanyInvoiceOrgSettingsDto>>;
