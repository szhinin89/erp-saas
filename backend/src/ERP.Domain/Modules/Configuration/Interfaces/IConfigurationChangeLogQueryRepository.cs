using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Interfaces;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: filtros de consulta del historial. Todos opcionales salvo el scope
/// tenant/company, que siempre lo resuelve el llamador desde el contexto autenticado — nunca del
/// query string (Principio de la arquitectura objetivo: tenant/company nunca vienen del body/query
/// como autoridad).
/// </summary>
public sealed record ConfigurationChangeLogQuery(
    Guid TenantId,
    Guid CompanyId,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Key = null,
    OrgScope? Scope = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 50
);

/// <summary>
/// CONFIG-FOUNDATION-P2-01: lectura de solo consulta del historial — separado de
/// IConfigurationChangeLogger (que solo escribe) para no mezclar responsabilidades de lectura
/// administrativa con el punto de escritura crítico.
/// </summary>
public interface IConfigurationChangeLogQueryRepository
{
    Task<(IReadOnlyList<ConfigurationChangeLog> Items, int TotalCount)> GetAsync(
        ConfigurationChangeLogQuery query,
        CancellationToken cancellationToken = default
    );
}
