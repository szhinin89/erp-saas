namespace ERP.Domain.Common;

public interface ISubscriberScopedEntity
{
    Guid SubscriberId { get; }
}
