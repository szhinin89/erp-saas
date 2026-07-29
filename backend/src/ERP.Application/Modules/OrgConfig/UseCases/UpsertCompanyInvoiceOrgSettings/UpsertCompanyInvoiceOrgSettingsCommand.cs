using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

public sealed record UpsertCompanyInvoiceOrgSettingsCommand(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultPaymentTermId
) : IRequest<Result<CompanyInvoiceOrgSettingsDto>>;
