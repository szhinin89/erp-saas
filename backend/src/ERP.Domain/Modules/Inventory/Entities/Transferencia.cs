using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Transferencia de stock entre dos bodegas del mismo tenant.
/// Flujo: Borrador → Confirmado | Cancelado.
/// El stock solo se mueve al Confirmar (operación atómica).
/// </summary>
public sealed class Transferencia : AuditableEntity, ITenantEntity
{
    public const int NumeroMaxLen      = 20;
    public const int EstadoMaxLen      = 20;
    public const int MotivoMaxLen      = 500;
    public const int ObservacionMaxLen = 1000;

    private readonly List<TransferenciaDetalle> _detalles = new();

    public int      Secuencial           { get; private set; }
    public string   NumeroTransferencia  { get; private set; } = null!;
    public Guid     BodegaOrigenId       { get; private set; }
    public Guid     BodegaDestinoId      { get; private set; }
    public DateTime FechaTransferencia   { get; private set; }
    public string   Estado               { get; private set; } = "Borrador";
    public string?  Motivo               { get; private set; }
    public string?  Observaciones        { get; private set; }
    public DateTime? FechaConfirmacion   { get; private set; }
    public Guid?    ConfirmadoPor        { get; private set; }

    public Bodega BodegaOrigen  { get; private set; } = null!;
    public Bodega BodegaDestino { get; private set; } = null!;

    public IReadOnlyList<TransferenciaDetalle> Detalles => _detalles.AsReadOnly();

    // English aliases for gradual migration
    public string StockTransferNumber => NumeroTransferencia;
    public Guid SourceWarehouseId => BodegaOrigenId;
    public Guid TargetWarehouseId => BodegaDestinoId;
    public DateTime TransferDate => FechaTransferencia;
    public string Status => Estado;
    public IReadOnlyList<TransferenciaDetalle> Lines => Detalles;

    private Transferencia() { }

    public static Transferencia Create(
        Guid    tenantId,
        int     secuencial,
        Guid    bodegaOrigenId,
        Guid    bodegaDestinoId,
        string? motivo,
        string? observaciones,
        Guid    createdBy)
    {
        var t = new Transferencia
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            Secuencial          = secuencial,
            NumeroTransferencia = $"TR-{secuencial:D4}",
            BodegaOrigenId      = bodegaOrigenId,
            BodegaDestinoId     = bodegaDestinoId,
            FechaTransferencia  = DateTime.UtcNow,
            Estado              = "Borrador",
            Motivo              = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            Observaciones       = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
        };
        t.SetCreated(createdBy);
        return t;
    }

    public void AgregarDetalle(TransferenciaDetalle detalle)
    {
        if (detalle is null) throw new ArgumentNullException(nameof(detalle));
        _detalles.Add(detalle);
    }

    public void AddLine(TransferenciaDetalle line) => AgregarDetalle(line);

    public void Confirmar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException(
                $"Solo se puede confirmar una transferencia en Borrador (estado actual: {Estado}).");
        Estado             = "Confirmado";
        FechaConfirmacion  = DateTime.UtcNow;
        ConfirmadoPor      = userId;
        SetUpdated(userId);
    }

    public void Confirm(Guid userId) => Confirmar(userId);

    public void Cancelar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException(
                $"Solo se puede cancelar una transferencia en Borrador (estado actual: {Estado}).");
        Estado = "Cancelado";
        SetUpdated(userId);
    }

    public void Cancel(Guid userId) => Cancelar(userId);
}
