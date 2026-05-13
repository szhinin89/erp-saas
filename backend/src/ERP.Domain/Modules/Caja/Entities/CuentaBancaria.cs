using ERP.Domain.Common;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>Cuenta corriente o de ahorros del tenant, vinculable a una cuenta del plan contable.</summary>
public sealed class CuentaBancaria : MasterEntity, ITenantEntity
{
    public const int NombreMaxLen       = 200;
    public const int NumeroCuentaMaxLen = 40;
    public const int TipoCuentaMaxLen   = 40;
    public const int MonedaMaxLen       = 3;

    public string Nombre { get; private set; } = null!;
    public string NumeroCuenta { get; private set; } = null!;
    public string TipoCuenta { get; private set; } = null!;
    public string Moneda { get; private set; } = "USD";
    public decimal SaldoInicial { get; private set; }
    public decimal SaldoActual { get; private set; }
    public Guid? CuentaContableId { get; private set; }

    private CuentaBancaria() { }

    public static CuentaBancaria Create(
        Guid tenantId,
        string nombre,
        string numeroCuenta,
        string tipoCuenta,
        string moneda,
        decimal saldoInicial,
        Guid createdBy,
        Guid? cuentaContableId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(numeroCuenta))
            throw new ArgumentException("El número de cuenta es obligatorio.", nameof(numeroCuenta));
        if (string.IsNullOrWhiteSpace(tipoCuenta))
            throw new ArgumentException("El tipo de cuenta es obligatorio.", nameof(tipoCuenta));

        var m = (moneda ?? "USD").Trim().ToUpperInvariant();
        if (m.Length != 3)
            throw new ArgumentException("La moneda debe ser un código ISO de 3 letras.", nameof(moneda));

        var c = new CuentaBancaria
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            Nombre           = nombre.Trim(),
            NumeroCuenta     = numeroCuenta.Trim(),
            TipoCuenta       = tipoCuenta.Trim(),
            Moneda           = m,
            SaldoInicial     = saldoInicial,
            SaldoActual      = saldoInicial,
            CuentaContableId = cuentaContableId,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string nombre,
        string numeroCuenta,
        string tipoCuenta,
        string moneda,
        Guid? cuentaContableId,
        Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        Nombre           = nombre.Trim();
        NumeroCuenta     = numeroCuenta.Trim();
        TipoCuenta       = tipoCuenta.Trim();
        Moneda           = (moneda ?? "USD").Trim().ToUpperInvariant();
        CuentaContableId = cuentaContableId;
        SetUpdated(updatedBy);
    }

    public void AjustarSaldoActual(decimal saldo, Guid updatedBy)
    {
        SaldoActual = saldo;
        SetUpdated(updatedBy);
    }

    public void IncrementarSaldo(decimal delta, Guid updatedBy)
    {
        SaldoActual += delta;
        SetUpdated(updatedBy);
    }
}
