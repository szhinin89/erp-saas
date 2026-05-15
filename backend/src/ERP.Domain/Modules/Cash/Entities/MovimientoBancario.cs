using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

/// <summary>Línea de extracto bancario importada; puede conciliarse con un asiento contable.</summary>
public sealed class MovimientoBancario : BaseEntity
{
    public const int DescripcionMaxLen = 500;
    public const int TipoMaxLen        = 20;
    public const int ReferenciaMaxLen  = 120;
    public const int EstadoMaxLen      = 20;

    public Guid ExtractoBancarioId { get; private set; }
    public DateTime Fecha { get; private set; }
    public string Descripcion { get; private set; } = null!;
    public decimal Monto { get; private set; }
    public string Tipo { get; private set; } = null!;
    public string Referencia { get; private set; } = "";
    public Guid? AsientoContableId { get; private set; }
    public string Estado { get; private set; } = "Pendiente";

    private MovimientoBancario() { }

    internal static MovimientoBancario Create(
        Guid extractoBancarioId,
        Guid tenantId,
        DateTime fecha,
        string descripcion,
        decimal monto,
        string tipo,
        string? referencia)
    {
        if (monto <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.", nameof(monto));
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("El tipo (Debito/Credito) es obligatorio.", nameof(tipo));

        return new MovimientoBancario
        {
            Id                   = Guid.NewGuid(),
            TenantId             = tenantId,
            ExtractoBancarioId   = extractoBancarioId,
            Fecha                = fecha,
            Descripcion          = descripcion.Trim(),
            Monto                = monto,
            Tipo                 = tipo.Trim(),
            Referencia           = referencia?.Trim() ?? "",
            AsientoContableId    = null,
            Estado               = "Pendiente",
        };
    }

    public void Conciliar(Guid asientoContableId)
    {
        if (Estado == "Conciliado")
            throw new InvalidOperationException("El movimiento ya está conciliado.");
        AsientoContableId = asientoContableId;
        Estado            = "Conciliado";
    }

    public void Desconciliar()
    {
        AsientoContableId = null;
        Estado            = "Pendiente";
    }
}
