using ERP.Application.Common;

namespace ERP.API.Tests.Support;

internal sealed class MutableCurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; set; }
    public string? Slug { get; set; }
}
