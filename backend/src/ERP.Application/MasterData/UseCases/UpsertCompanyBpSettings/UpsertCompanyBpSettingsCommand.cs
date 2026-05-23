using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;

/// <summary>
/// Crea o actualiza las condiciones comerciales de un BusinessPartner para la Company activa.
/// ICompanyScopedRequest: requiere company_id en el JWT.
/// Las condiciones (CreditLimit, PaymentDays) varían por empresa — nunca van en BusinessPartner.
/// </summary>
public sealed record UpsertCompanyBpSettingsCommand(
    Guid     BusinessPartnerId,
    decimal? CreditLimit,
    short    PaymentDays,
    bool     IsBlocked) : IRequest<Result<bool>>, ICompanyScopedRequest
{
    public Guid? ExplicitCompanyId => null; // usa company_id del JWT
}
