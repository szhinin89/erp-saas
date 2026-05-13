using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventario.Entities;

/// <summary>
/// Ajuste manual de stock en una bodega: puede ser un incremento (sobrante, donación)
/// o una disminución (merma, robo, pérdida). Un ajuste por producto.
/// Flujo: Borrador → Ejecutado | Cancelado.
/// El stock solo se mueve al Ejecutar.
/// </summary>
public sealed class AjusteInventario : AuditableEntity, ITenantEntity
{
    public const int NumeroMaxLen     = 20;
    public const int TipoAjusteMaxLen = 20;
    public const int MotivoMaxLen     = 200;
    public const int ObsMaxLen        = 1000;
    public const int EstadoMaxLen     = 20;
    public const int NombreMaxLen     = 300;

    public int      Secuencial     { get; private set; }
    public string   NumeroAjuste   { get; private set; } = null!;
    public Guid     BodegaId       { get; private set; }
    public string   BodegaNombre   { get; private set; } = null!;
    public Guid     ProductoId     { get; private set; }
    public string   ProductoNombre { get; private set; } = null!;

    /// <summary>Positivo = incremento de stock; negativo = disminución.</summary>
    public decimal  CantidadAjuste { get; private set; }

    /// <summary>"Incremento" cuando CantidadAjuste > 0; "Disminucion" cuando < 0.</summary>
    public string   TipoAjuste     { get; private set; } = null!;

    public string   Motivo         { get; private set; } = null!;
    public string?  Observaciones  { get; private set; }
    public DateTime FechaAjuste    { get; private set; }
    public string   Estado         { get; private set; } = "Borrador";
    public DateTime? FechaEjecucion { get; private set; }
    public Guid?    EjecutadoPor   { get; private set; }

    private AjusteInventario() { }

    public static AjusteInventario Create(
        Guid    tenantId,
        int     secuencial,
        Guid    bodegaId,
        string  bodegaNombre,
        Guid    productoId,
        string  productoNombre,
        decimal cantidadAjuste,
        string  motivo,
        string? observaciones,
        Guid    createdBy)
    {
        if (cantidadAjuste == 0)
            throw new ArgumentException("La cantidad de ajuste no puede ser cero.", nameof(cantidadAjuste));
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo es obligatorio.", nameof(motivo));

        var a = new AjusteInventario
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            Secuencial     = secuencial,
            NumeroAjuste   = $"AJ-{secuencial:D4}",
            BodegaId       = bodegaId,
            BodegaNombre   = bodegaNombre.Trim(),
            ProductoId     = productoId,
            ProductoNombre = productoNombre.Trim(),
            CantidadAjuste = cantidadAjuste,
            TipoAjuste     = cantidadAjuste > 0 ? "Incremento" : "Disminucion",
            Motivo         = motivo.Trim(),
            Observaciones  = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
            FechaAjuste    = DateTime.UtcNow,
            Estado         = "Borrador",
        };
        a.SetCreated(createdBy);
        return a;
    }

    public void Ejecutar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException(
                $"Solo se puede ejecutar un ajuste en Borrador (estado actual: {Estado}).");
        Estado         = "Ejecutado";
        FechaEjecucion = DateTime.UtcNow;
        EjecutadoPor   = userId;
        SetUpdated(userId);
    }

    public void Cancelar(Guid userId)
    {
        if (Estado != "Borrador")
            throw new InvalidOperationException(
                $"Solo se puede cancelar un ajuste en Borrador (estado actual: {Estado}).");
        Estado = "Cancelado";
        SetUpdated(userId);
    }
}
