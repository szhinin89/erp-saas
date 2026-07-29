using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Audit;

/// <summary>
/// Resuelve <see cref="IAuditContext"/> desde el contexto HTTP del request en curso,
/// reutilizando <see cref="ICurrentTenant"/>/<see cref="ICurrentCompany"/>/<see cref="ICurrentUser"/>
/// ya existentes en vez de leer claims por su cuenta. El correlationId se lee de
/// <c>HttpContext.Items["CorrelationId"]</c> — misma clave que
/// <c>RequestCorrelationMiddleware</c> (ERP.API) publica; Infrastructure no puede
/// referenciar ERP.API, así que se lee por convención de clave en vez de por tipo.
/// </summary>
public sealed class HttpAuditContext : IAuditContext
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HttpAuditContext> _logger;

    public HttpAuditContext(
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HttpAuditContext> logger
    )
    {
        _tenant = tenant;
        _company = company;
        _user = user;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid TenantId => _tenant.TenantId;
    public Guid CompanyId => _company.CompanyId;

    public AuditActor Actor
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var requestId = httpContext?.TraceIdentifier;
            var correlationId =
                httpContext?.Items.TryGetValue("CorrelationId", out var value) == true
                && value is string id
                    ? id
                    : requestId;

            // Sin HttpContext (job Hangfire/background) no hay usuario real que resolver — el
            // actor es el sistema, nunca "Unknown" (que implicaría un usuario autenticado cuyo
            // nombre no se pudo leer, un caso distinto). Ver CLAUDE.md, deuda técnica #2.
            if (httpContext is null)
            {
                return new AuditActor(
                    TenantId: _tenant.TenantId,
                    UserId: _user.UserId,
                    UserName: "System",
                    FullName: null,
                    Email: null,
                    RoleName: null,
                    CorrelationId: correlationId,
                    RequestId: requestId,
                    Source: AuditSource.System
                );
            }

            // Snapshot al momento del evento: FullName/Email vienen de claims embebidas en el
            // JWT al emitirlo (AccessTokenService), no de una consulta en vivo a la tabla de
            // usuarios — así el nombre auditado no cambia si el usuario edita su perfil después.
            var userName = _user.FullName ?? _user.Username ?? _user.Email;
            if (string.IsNullOrWhiteSpace(userName) && _user.IsAuthenticated)
            {
                _logger.LogWarning(
                    "No se pudo resolver el nombre visible del usuario {UserId} para auditoría; se usa 'Unknown'.",
                    _user.UserId
                );
                userName = "Unknown";
            }

            return new AuditActor(
                TenantId: _tenant.TenantId,
                UserId: _user.UserId,
                UserName: string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName,
                FullName: _user.FullName,
                Email: _user.Email,
                RoleName: _user.Role,
                CorrelationId: correlationId,
                RequestId: requestId,
                Source: AuditSource.UserAction
            );
        }
    }
}
