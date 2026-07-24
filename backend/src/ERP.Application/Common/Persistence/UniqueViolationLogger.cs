using Microsoft.Extensions.Logging;

namespace ERP.Application.Common.Persistence;

/// <summary>Logging estructurado para violaciones UNIQUE (sin datos sensibles).</summary>
public static partial class UniqueViolationLogger
{
    public static void LogUniqueViolation(
        ILogger logger,
        string handlerName,
        DatabaseUniqueViolationInfo violation,
        Guid tenantId,
        Guid companyId,
        string? correlationId = null)
    {
        LogUniqueViolationCore(
            logger,
            handlerName,
            tenantId,
            companyId,
            violation.ConstraintName ?? "",
            violation.TableName ?? "",
            violation.SqlState,
            correlationId ?? "none");
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Unique violation in {HandlerName} tenantId={TenantId} companyId={CompanyId} " +
                  "constraint={ConstraintName} table={TableName} sqlState={SqlState} correlationId={CorrelationId}")]
    private static partial void LogUniqueViolationCore(
        ILogger logger,
        string handlerName,
        Guid tenantId,
        Guid companyId,
        string constraintName,
        string tableName,
        string sqlState,
        string correlationId);
}
