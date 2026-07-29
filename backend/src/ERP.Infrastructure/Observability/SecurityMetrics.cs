using ERP.Application.Common.Security;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ERP.Infrastructure.Observability;

public sealed class SecurityMetrics : ISecurityMetrics
{
    public const string MeterName = "ERP.Security";
    private static readonly Meter Meter = new(MeterName);

    private readonly Counter<long> _crossCompanyDenied;
    private readonly Counter<long> _membershipValidationFailed;
    private readonly Counter<long> _invalidCompanyContext;
    private readonly Counter<long> _jwtRefreshRevoked;
    private readonly Counter<long> _permissionDenied;
    private readonly Counter<long> _dualWriteFailed;
    private readonly Counter<long> _syncInconsistency;
    private readonly Counter<long> _backgroundContextLeak;
    private readonly Counter<long> _namespaceFallbackUsed;

    public SecurityMetrics()
    {
        _crossCompanyDenied = Meter.CreateCounter<long>("security.cross_company_denied");
        _membershipValidationFailed = Meter.CreateCounter<long>("security.membership_validation_failed");
        _invalidCompanyContext = Meter.CreateCounter<long>("security.invalid_company_context");
        _jwtRefreshRevoked = Meter.CreateCounter<long>("security.jwt_refresh_revoked");
        _permissionDenied = Meter.CreateCounter<long>("security.permission_denied");
        _dualWriteFailed = Meter.CreateCounter<long>("masterdata.dualwrite_failed");
        _syncInconsistency = Meter.CreateCounter<long>("masterdata.sync_inconsistency");
        _backgroundContextLeak = Meter.CreateCounter<long>("background.context_leak_detected");
        _namespaceFallbackUsed = Meter.CreateCounter<long>("security.namespace_fallback_used");
    }

    public void RecordCrossCompanyDenied(SecurityMetricTags? tags = null)
        => Add(_crossCompanyDenied, tags);

    public void RecordMembershipValidationFailed(SecurityMetricTags? tags = null)
        => Add(_membershipValidationFailed, tags);

    public void RecordInvalidCompanyContext(SecurityMetricTags? tags = null)
        => Add(_invalidCompanyContext, tags);

    public void RecordJwtRefreshRevoked(SecurityMetricTags? tags = null)
        => Add(_jwtRefreshRevoked, tags);

    public void RecordPermissionDenied(SecurityMetricTags? tags = null)
        => Add(_permissionDenied, tags);

    public void RecordMasterDataDualWriteFailed(SecurityMetricTags? tags = null)
        => Add(_dualWriteFailed, tags);

    public void RecordMasterDataSyncInconsistency(SecurityMetricTags? tags = null)
        => Add(_syncInconsistency, tags);

    public void RecordBackgroundContextLeakDetected(SecurityMetricTags? tags = null)
        => Add(_backgroundContextLeak, tags);

    public void RecordNamespaceFallbackUsed(SecurityMetricTags? tags = null)
        => Add(_namespaceFallbackUsed, tags);

    private static void Add(Counter<long> counter, SecurityMetricTags? tags)
    {
        if (tags is null)
        {
            counter.Add(1);
            return;
        }

        var tagList = new TagList();
        if (tags.TenantId is Guid sub && sub != Guid.Empty)
            tagList.Add("tenant_id", sub.ToString("D"));
        if (tags.CompanyId is Guid co && co != Guid.Empty)
            tagList.Add("company_id", co.ToString("D"));
        if (!string.IsNullOrWhiteSpace(tags.Endpoint))
            tagList.Add("endpoint", tags.Endpoint);
        if (!string.IsNullOrWhiteSpace(tags.RequestType))
            tagList.Add("request_type", tags.RequestType);
        if (!string.IsNullOrWhiteSpace(tags.CorrelationId))
            tagList.Add("correlation_id", tags.CorrelationId);

        counter.Add(1, tagList);
    }
}
