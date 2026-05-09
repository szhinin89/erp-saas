using ERP.Domain.Common;

namespace ERP.Domain.Gastos.Entities;

/// <summary>
/// Gasto o factura de egreso del tenant.
/// Soft delete vía <see cref="MasterEntity"/>.
/// </summary>
public sealed class GastoFactura : MasterEntity, ITenantEntity
{
    public const int NumeroFacturaMaxLen = 50;
    public const int ConceptoMaxLen      = 200;
    public const int CategoriaMaxLen     = 80;
    public const int ObservacionesMaxLen = 1000;

    public Guid?    ProveedorId     { get; private set; }
    public string?  NumeroFactura   { get; private set; }
    public DateTime FechaFactura    { get; private set; }
    public string   Concepto        { get; private set; } = null!;
    public string   Categoria       { get; private set; } = null!;
    public decimal  Monto           { get; private set; }
    public decimal  Iva             { get; private set; }
    public decimal  Total           { get; private set; }
    public string?  Observaciones   { get; private set; }

    private GastoFactura() { }

    public static GastoFactura Create(
        Guid     tenantId,
        Guid?    proveedorId,
        string?  numeroFactura,
        DateTime fechaFactura,
        string   concepto,
        string   categoria,
        decimal  monto,
        decimal  ivaPorcentaje,
        string?  observaciones,
        Guid     createdBy)
    {
        if (monto < 0)
            throw new ArgumentException("El monto no puede ser negativo.", nameof(monto));

        var iva   = monto * ivaPorcentaje / 100m;
        var total = monto + iva;

        var g = new GastoFactura
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ProveedorId    = proveedorId,
            NumeroFactura  = Trim(numeroFactura),
            FechaFactura   = fechaFactura,
            Concepto       = concepto.Trim(),
            Categoria      = categoria.Trim(),
            Monto          = monto,
            Iva            = iva,
            Total          = total,
            Observaciones  = Trim(observaciones),
        };
        g.SetCreated(createdBy);
        return g;
    }

    public void Update(
        Guid?    proveedorId,
        string?  numeroFactura,
        DateTime fechaFactura,
        string   concepto,
        string   categoria,
        decimal  monto,
        decimal  ivaPorcentaje,
        string?  observaciones,
        Guid     updatedBy)
    {
        if (monto < 0)
            throw new ArgumentException("El monto no puede ser negativo.", nameof(monto));

        var iva   = monto * ivaPorcentaje / 100m;

        ProveedorId   = proveedorId;
        NumeroFactura = Trim(numeroFactura);
        FechaFactura  = fechaFactura;
        Concepto      = concepto.Trim();
        Categoria     = categoria.Trim();
        Monto         = monto;
        Iva           = iva;
        Total         = monto + iva;
        Observaciones = Trim(observaciones);
        SetUpdated(updatedBy);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
