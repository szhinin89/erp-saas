using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Reserva de stock para un pedido pendiente. Previene que el stock disponible
/// caiga por debajo de lo comprometido antes de despachar.
/// </summary>
public sealed class StockReservation : AuditableEntity, ISubscriberScopedEntity
{
    public const string StatusPending   = "Pending";
    public const string StatusFulfilled = "Fulfilled";
    public const string StatusCancelled = "Cancelled";

    public Guid    ProductId   { get; private set; }
    public Guid    WarehouseId { get; private set; }
    public Guid?   OrderId     { get; private set; }
    public decimal Quantity    { get; private set; }
    public string  Status      { get; private set; } = StatusPending;
    public DateTime ExpiresAt  { get; private set; }
    public string?  Notes      { get; private set; }

    private StockReservation() { }

    public static StockReservation Create(
        Guid     subscriberId,
        Guid     productId,
        Guid     warehouseId,
        decimal  quantity,
        DateTime expiresAt,
        Guid     createdBy,
        Guid?    orderId = null,
        string?  notes   = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("La cantidad a reservar debe ser mayor a cero.", nameof(quantity));
        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("La fecha de expiración debe ser futura.", nameof(expiresAt));

        var r = new StockReservation
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ProductId    = productId,
            WarehouseId  = warehouseId,
            OrderId      = orderId,
            Quantity     = quantity,
            Status       = StatusPending,
            ExpiresAt    = expiresAt,
            Notes        = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        r.SetCreated(createdBy);
        return r;
    }

    public void Fulfill(Guid userId)
    {
        if (Status != StatusPending)
            throw new InvalidOperationException($"Only Pending reservations can be fulfilled (current: {Status}).");
        Status = StatusFulfilled;
        SetUpdated(userId);
    }

    public void Cancel(Guid userId)
    {
        if (Status == StatusCancelled)
            throw new InvalidOperationException("Reservation is already cancelled.");
        if (Status == StatusFulfilled)
            throw new InvalidOperationException("Cannot cancel a fulfilled reservation.");
        Status = StatusCancelled;
        SetUpdated(userId);
    }

    public bool IsExpired => Status == StatusPending && DateTime.UtcNow > ExpiresAt;
}
