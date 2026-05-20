namespace ERP.Domain.Common;

public abstract class BaseEntity : IMustHaveSubscriber
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid SubscriberId { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid id, Guid subscriberId)
    {
        Id = id;
        SubscriberId = subscriberId;
    }
}
