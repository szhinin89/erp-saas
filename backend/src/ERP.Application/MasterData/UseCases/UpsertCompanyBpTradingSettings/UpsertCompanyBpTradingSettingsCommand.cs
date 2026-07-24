using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpTradingSettings;

/// <summary>
/// Crea o actualiza la configuración comercial del BP en la empresa activa.
/// Company-scoped: requiere company_id en el JWT.
/// Semántica Upsert: si ya existe → Update, si no → Create.
/// </summary>
public sealed record UpsertCompanyBpTradingSettingsCommand(
    Guid    BusinessPartnerId,
    decimal CreditLimit,
    int     PaymentDays,
    string  CreditCurrencyCode = "USD")
    : IRequest<Result<CompanyBpTradingSettingsDto>>, ICompanyScopedRequest;
