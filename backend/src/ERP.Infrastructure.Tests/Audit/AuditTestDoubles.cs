using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Audit;

namespace ERP.Infrastructure.Tests.Audit;

/// <summary>Dobles de prueba compartidos por las suites de integración de auditoría por dominio.</summary>
internal sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
{
    public Guid TenantId => tenantId();
    public string? Slug => null;
}

internal sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
{
    public Guid CompanyId => companyId();
    public bool IsAuthenticated => true;
    public bool HasCompanyContext => true;
}

/// <summary>
/// <paramref name="userName"/> es un accessor (no un valor fijo) a propósito: permite a los
/// tests simular que el "perfil actual" del usuario cambia entre dos eventos, para probar que
/// <see cref="AuditActor"/> queda persistido como snapshot y nunca se resincroniza — cada
/// llamada a <see cref="Actor"/> refleja el nombre vigente en ESE momento, exactamente como
/// haría <c>HttpAuditContext</c> leyendo el JWT del request en curso.
/// </summary>
internal sealed class FixedAuditContext(
    Func<Guid> tenantId, Func<Guid> companyId, Guid userId, Func<string>? userName = null) : IAuditContext
{
    private readonly Func<string> _userName = userName ?? (() => "Test User");

    public Guid TenantId => tenantId();
    public Guid CompanyId => companyId();
    public AuditActor Actor => new(
        TenantId: tenantId(),
        UserId: userId,
        UserName: _userName(),
        FullName: _userName(),
        Email: "test.user@example.com",
        RoleName: "Admin",
        CorrelationId: "test-correlation",
        RequestId: "test-request",
        Source: AuditSource.UserAction);
}
