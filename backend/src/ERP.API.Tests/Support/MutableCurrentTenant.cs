using ERP.Application.Common;

namespace ERP.API.Tests.Support;

/// <summary>Subscriber fijo para integración: asignar <see cref="SubscriberId"/> tras persistir el <c>Tenant</c>.</summary>
internal sealed class MutableCurrentSubscriber : ICurrentSubscriber
{
    public Guid SubscriberId { get; set; }

    public bool IsAuthenticated => SubscriberId != Guid.Empty;
}
