using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

/// <summary>Extracto importado por periodo; agrupa movimientos bancarios.</summary>
public sealed class ExtractoBancario : AuditableEntity, ITenantEntity
{
    private readonly List<MovimientoBancario> _movimientos = new();

    public IReadOnlyList<MovimientoBancario> Movimientos => _movimientos.AsReadOnly();

    public Guid CuentaBancariaId { get; private set; }
    public DateTime PeriodoDesde { get; private set; }
    public DateTime PeriodoHasta { get; private set; }
    public decimal SaldoInicialExtracto { get; private set; }
    public decimal SaldoFinalExtracto { get; private set; }
    public DateTime FechaCarga { get; private set; }
    public bool Conciliado { get; private set; }

    private ExtractoBancario() { }

    public static ExtractoBancario Create(
        Guid tenantId,
        Guid cuentaBancariaId,
        DateTime periodoDesde,
        DateTime periodoHasta,
        decimal saldoInicialExtracto,
        decimal saldoFinalExtracto,
        Guid createdBy)
    {
        if (periodoHasta < periodoDesde)
            throw new ArgumentException("El periodo hasta no puede ser anterior al desde.");

        var e = new ExtractoBancario
        {
            Id                     = Guid.NewGuid(),
            TenantId               = tenantId,
            CuentaBancariaId       = cuentaBancariaId,
            PeriodoDesde           = periodoDesde,
            PeriodoHasta           = periodoHasta,
            SaldoInicialExtracto   = saldoInicialExtracto,
            SaldoFinalExtracto     = saldoFinalExtracto,
            FechaCarga             = DateTime.UtcNow,
            Conciliado             = false,
        };
        e.SetCreated(createdBy);
        return e;
    }

    public void AgregarMovimiento(
        DateTime fecha,
        string descripcion,
        decimal monto,
        string tipo,
        string? referencia,
        Guid updatedBy)
    {
        var m = MovimientoBancario.Create(Id, TenantId, fecha, descripcion, monto, tipo, referencia);
        _movimientos.Add(m);
        Conciliado = false;
        SetUpdated(updatedBy);
    }

    public void MarcarConciliadoSiCompleto(Guid updatedBy)
    {
        if (_movimientos.Count == 0)
            return;
        if (_movimientos.All(x => x.Estado == "Conciliado"))
        {
            Conciliado = true;
            SetUpdated(updatedBy);
        }
        else
        {
            Conciliado = false;
            SetUpdated(updatedBy);
        }
    }

    public void DesmarcarConciliadoExtracto(Guid updatedBy)
    {
        Conciliado = false;
        SetUpdated(updatedBy);
    }
}
