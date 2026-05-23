using Microsoft.Extensions.Logging;

namespace ERP.Application.Common.Persistence;

/// <summary>Logging estructurado para violaciones UNIQUE (sin datos sensibles).</summary>
public static class UniqueViolationLogger
{
    public static void LogUniqueViolation(
        ILogger logger,
        string handlerName,
        DatabaseUniqueViolationInfo violation,
        Guid subscriberId,
        Guid companyId,
        string? correlationId = null)
    {
        logger.LogWarning(
            "Unique violation in {HandlerName} subscriberId={SubscriberId} companyId={CompanyId} " +
            "constraint={ConstraintName} table={TableName} sqlState={SqlState} correlationId={CorrelationId}",
            handlerName,
            subscriberId,
            companyId,
            violation.ConstraintName,
            violation.TableName,
            violation.SqlState,
            correlationId ?? "none");
    }
}
