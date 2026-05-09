using ERP.Application.Common;

namespace ERP.API.Tests.Support;

/// <summary>Tenant fijo para integración: asignar <see cref="TenantId"/> tras persistir el <c>Tenant</c>.</summary>
internal sealed class MutableCurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; set; }

    public bool IsAuthenticated => TenantId != Guid.Empty;
}
