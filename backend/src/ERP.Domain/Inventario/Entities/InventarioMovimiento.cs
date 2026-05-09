using ERP.Domain.Common;
using ERP.Domain.Inventario.Enums;

namespace ERP.Domain.Inventario.Entities;

/// <summary>
/// Registro inmutable de cada movimiento de stock (entrada, salida, ajuste, etc.).
/// </summary>
public sealed class InventarioMovimiento : AuditableEntity, ITenantEntity
{
    public const int ReferenciaMaxLen         = 100;
    public const int DocumentoOrigenTipoMaxLen = 50;

    public Guid                     ProductoId           { get; private set; }
    public Guid                     BodegaId             { get; private set; }
    public TipoMovimientoInventario TipoMovimiento       { get; private set; }
    public decimal                  Cantidad             { get; private set; }
    public decimal                  CantidadAnterior     { get; private set; }
    public decimal                  CantidadResultante   { get; private set; }
    public string?                  Referencia           { get; private set; }
    public Guid?                    DocumentoOrigenId    { get; private set; }
    public string?                  DocumentoOrigenTipo  { get; private set; }

    private InventarioMovimiento() { }

    public static InventarioMovimiento Create(
        Guid                     tenantId,
        Guid                     productoId,
        Guid                     bodegaId,
        TipoMovimientoInventario tipoMovimiento,
        decimal                  cantidad,
        decimal                  cantidadAnterior,
        string?                  referencia,
        Guid?                    documentoOrigenId,
        string?                  documentoOrigenTipo,
        Guid                     createdBy)
    {
        var m = new InventarioMovimiento
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            ProductoId          = productoId,
            BodegaId            = bodegaId,
            TipoMovimiento      = tipoMovimiento,
            Cantidad            = cantidad,
            CantidadAnterior    = cantidadAnterior,
            CantidadResultante  = cantidadAnterior + cantidad,
            Referencia          = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim(),
            DocumentoOrigenId   = documentoOrigenId,
            DocumentoOrigenTipo = string.IsNullOrWhiteSpace(documentoOrigenTipo) ? null : documentoOrigenTipo.Trim(),
        };
        m.SetCreated(createdBy);
        return m;
    }
}
