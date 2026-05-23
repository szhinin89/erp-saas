namespace ERP.Application.Common.Security;

/// <summary>
/// Métricas de seguridad y aislamiento multiempresa (OpenTelemetry).
/// Tags permitidos: subscriber_id, company_id, endpoint, request_type, correlation_id.
/// </summary>
public interface ISecurityMetrics
{
    void RecordCrossCompanyDenied(SecurityMetricTags? tags = null);
    void RecordMembershipValidationFailed(SecurityMetricTags? tags = null);
    void RecordInvalidCompanyContext(SecurityMetricTags? tags = null);
    void RecordJwtRefreshRevoked(SecurityMetricTags? tags = null);
    void RecordPermissionDenied(SecurityMetricTags? tags = null);
    void RecordMasterDataDualWriteFailed(SecurityMetricTags? tags = null);
    void RecordMasterDataSyncInconsistency(SecurityMetricTags? tags = null);
    void RecordBackgroundContextLeakDetected(SecurityMetricTags? tags = null);
    void RecordNamespaceFallbackUsed(SecurityMetricTags? tags = null);
}
