using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

/// <summary>
/// Guarda las preferencias de venta de la empresa en una sola transacción.
/// <paramref name="DefaultPriceList"/> ausente (null) = no se toca la lista predeterminada;
/// presente = se reasigna (<c>PriceListId = null</c> la quita). La lista sigue viviendo solo en
/// <c>PriceList.IsDefault</c> — nunca como OrgSetting.
/// </summary>
public sealed record UpsertCompanyInvoiceOrgSettingsCommand(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultPaymentTermId,
    CompanyDefaultPriceListChange? DefaultPriceList = null
) : IRequest<Result<CompanyInvoiceOrgSettingsDto>>, IRequiresCompanyContext;

/// <summary>Cambio explícito de la lista de precios predeterminada (null = sin lista; aplica el PVP).</summary>
public sealed record CompanyDefaultPriceListChange(Guid? PriceListId);
