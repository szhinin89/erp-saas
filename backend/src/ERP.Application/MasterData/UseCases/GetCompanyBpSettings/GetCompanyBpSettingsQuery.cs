using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetCompanyBpSettings;

/// <summary>
/// Obtiene las condiciones comerciales de un BP para la Company activa.
/// ICompanyScopedRequest: requiere company_id en el JWT.
/// </summary>
public sealed record GetCompanyBpSettingsQuery(Guid BusinessPartnerId)
    : IRequest<Result<CompanyBpSettingsDto?>>, ICompanyScopedRequest
{
    public Guid? ExplicitCompanyId => null;
}
