using ERP.Domain.Common;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>Fondo fijo para gastos menores; saldo se reduce con gastos y aumenta con reposiciones.</summary>
public sealed class CajaChica : MasterEntity, ITenantEntity
{
    public const int NombreMaxLen = 200;

    public string Nombre { get; private set; } = null!;
    public decimal SaldoAsignado { get; private set; }
    public decimal SaldoActual { get; private set; }
    public Guid? CuentaBancariaIdReposicion { get; private set; }
    public Guid? CuentaContableCajaId { get; private set; }

    private CajaChica() { }

    public static CajaChica Create(
        Guid tenantId,
        string nombre,
        decimal saldoAsignado,
        Guid createdBy,
        Guid? cuentaBancariaIdReposicion = null,
        Guid? cuentaContableCajaId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (saldoAsignado < 0)
            throw new ArgumentException("El saldo asignado no puede ser negativo.", nameof(saldoAsignado));

        var c = new CajaChica
        {
            Id                         = Guid.NewGuid(),
            TenantId                   = tenantId,
            Nombre                     = nombre.Trim(),
            SaldoAsignado              = saldoAsignado,
            SaldoActual                = saldoAsignado,
            CuentaBancariaIdReposicion = cuentaBancariaIdReposicion,
            CuentaContableCajaId       = cuentaContableCajaId,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string nombre,
        decimal saldoAsignado,
        Guid? cuentaBancariaIdReposicion,
        Guid? cuentaContableCajaId,
        Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        Nombre                     = nombre.Trim();
        SaldoAsignado              = saldoAsignado;
        CuentaBancariaIdReposicion = cuentaBancariaIdReposicion;
        CuentaContableCajaId       = cuentaContableCajaId;
        SetUpdated(updatedBy);
    }

    public void RegistrarGasto(decimal monto, Guid updatedBy)
    {
        if (monto <= 0)
            throw new ArgumentException("El monto del gasto debe ser mayor que cero.", nameof(monto));
        if (SaldoActual < monto)
            throw new InvalidOperationException("Saldo insuficiente en la caja chica.");
        SaldoActual -= monto;
        SetUpdated(updatedBy);
    }

    public void RegistrarReposicion(decimal monto, Guid updatedBy)
    {
        if (monto <= 0)
            throw new ArgumentException("El monto de reposición debe ser mayor que cero.", nameof(monto));
        SaldoActual += monto;
        SetUpdated(updatedBy);
    }

    public void AplicarSaldoTrasArqueo(decimal efectivoFisico, Guid updatedBy)
    {
        if (efectivoFisico < 0)
            throw new ArgumentException("El efectivo físico no puede ser negativo.", nameof(efectivoFisico));
        SaldoActual = efectivoFisico;
        SetUpdated(updatedBy);
    }
}
