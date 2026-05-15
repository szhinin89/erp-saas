using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

/// <summary>
/// Líneas de inventario a reingresar por devolución en nota de crédito autorizada.
/// </summary>
public sealed record NotaCreditoStockLine(Guid ProductoId, decimal Cantidad);

/// <summary>
/// Nota de crédito de venta autorizada por el SRI; el inventario reacciona en el handler.
/// </summary>
public sealed class NotaCreditoAutorizadaEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public Guid NotaId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public Guid BodegaId { get; }
    public string NumeroNota { get; }
    public IReadOnlyList<NotaCreditoStockLine> StockLines { get; }

    public NotaCreditoAutorizadaEvent(
        Guid notaId,
        Guid tenantId,
        Guid userId,
        Guid bodegaId,
        string numeroNota,
        IReadOnlyList<NotaCreditoStockLine> stockLines)
    {
        NotaId      = notaId;
        TenantId    = tenantId;
        UserId      = userId;
        BodegaId    = bodegaId;
        NumeroNota  = numeroNota;
        StockLines  = stockLines;
    }
}
