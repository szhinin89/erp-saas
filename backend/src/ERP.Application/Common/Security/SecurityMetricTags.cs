namespace ERP.Application.Common.Security;

/// <summary>Dimensiones permitidas en métricas de seguridad (sin PII ni secretos).</summary>
public sealed record SecurityMetricTags(
    Guid? TenantId = null,
    Guid? CompanyId = null,
    string? Endpoint = null,
    string? RequestType = null,
    string? CorrelationId = null
);
