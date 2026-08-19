using ERP.Application.Common;
using ERP.Application.Modules.Configuration.DTOs;
using ERP.Domain.Configuration.Enums;
using MediatR;

namespace ERP.Application.Modules.Configuration.UseCases.GetConfigurationChangeLog;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: consulta administrativa de solo lectura del historial de cambios
/// críticos de configuración. Tenant/Company se resuelven del contexto autenticado — nunca de
/// estos parámetros (que solo filtran, no autorizan).
/// </summary>
public sealed record GetConfigurationChangeLogQuery(
    string? EntityType = null,
    Guid? EntityId = null,
    string? Key = null,
    OrgScope? Scope = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<ConfigurationChangeLogPageDto>>, ICompanyScopedRequest;
