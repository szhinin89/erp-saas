using ERP.Domain.Common;
using ERP.Domain.Modules.Gastos.Enums;

namespace ERP.Domain.Modules.Gastos.Entities;

/// <summary>Factura / comprobante de gasto (incluye soporte XML SRI).</summary>
public sealed class GastoFactura : MasterEntity, ITenantEntity
{
    public const int ClaveAccesoLen       = 49;
    public const int XmlPathMaxLen        = 500;
    public const int NumeroFacturaMaxLen  = 50;
    public const int ConceptoMaxLen       = 200;
    public const int CategoriaGastoMaxLen = 80;
    public const int ObservacionesMaxLen  = 1000;
    public const int MotivoRechazoMaxLen  = 500;

    /// <summary>A partir de este total (USD), el alta manual exige comprobante XML.</summary>
    public const decimal TotalRequiereXml = 100m;

    public const decimal ToleranciaTotal = 0.01m;

    public string?      ClaveAcceso       { get; private set; }
    public DateTime     FechaEmision      { get; private set; }
    public Guid?        ProveedorId       { get; private set; }
    public string?     NumeroFactura    { get; private set; }
    public string       Concepto        { get; private set; } = null!;
    public string       CategoriaGasto    { get; private set; } = null!;
    public decimal      Subtotal          { get; private set; }
    public decimal      Impuesto          { get; private set; }
    public decimal      Total             { get; private set; }
    public EstadoGasto  Estado            { get; private set; } = EstadoGasto.Borrador;
    public string?     XmlPath          { get; private set; }
    public string?     Observaciones    { get; private set; }

    public Guid?      ValidadoPor      { get; private set; }
    public DateTime?  ValidadoEn       { get; private set; }
    public Guid?      AprobadoPor      { get; private set; }
    public DateTime?  AprobadoEn       { get; private set; }
    public Guid?      RechazadoPor     { get; private set; }
    public DateTime?  RechazadoEn      { get; private set; }
    public string?    MotivoRechazo    { get; private set; }
    public Guid?      AsientoContableId { get; private set; }

    /// <summary>Suma de totales de notas de crédito de proveedor aplicadas a este gasto.</summary>
    public decimal TotalNotasProveedorAplicado { get; private set; }

    private GastoFactura() { }

    public static GastoFactura CreateManual(
        Guid     tenantId,
        Guid?    proveedorId,
        DateTime fechaEmision,
        string   concepto,
        string   categoriaGasto,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string?  observaciones,
        Guid     createdBy)
    {
        ValidarMontos(subtotal, impuesto, total);
        if (total > TotalRequiereXml)
            throw new InvalidOperationException(
                $"Los gastos con total mayor a {TotalRequiereXml} deben registrarse con comprobante XML.");

        var g = new GastoFactura
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ProveedorId    = proveedorId,
            FechaEmision   = fechaEmision,
            Concepto       = concepto.Trim(),
            CategoriaGasto = categoriaGasto.Trim(),
            Subtotal       = subtotal,
            Impuesto       = impuesto,
            Total          = total,
            Estado         = EstadoGasto.Borrador,
            Observaciones  = Trim(observaciones),
        };
        g.SetCreated(createdBy);
        return g;
    }

    /// <summary>Alta desde XML ya parseado y ruta de archivo guardada.</summary>
    public static GastoFactura CreateFromXml(
        Guid     tenantId,
        Guid     proveedorId,
        string   claveAcceso,
        string?  numeroFactura,
        DateTime fechaEmision,
        string   concepto,
        string   categoriaGasto,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string   xmlPath,
        string?  observaciones,
        Guid     createdBy)
    {
        ValidarClaveAcceso(claveAcceso);
        ValidarMontos(subtotal, impuesto, total);

        var g = new GastoFactura
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            ProveedorId    = proveedorId,
            ClaveAcceso    = claveAcceso.Trim(),
            NumeroFactura  = Trim(numeroFactura),
            FechaEmision   = fechaEmision,
            Concepto       = concepto.Trim(),
            CategoriaGasto = categoriaGasto.Trim(),
            Subtotal       = subtotal,
            Impuesto       = impuesto,
            Total          = total,
            XmlPath        = xmlPath.Trim(),
            Estado         = EstadoGasto.Borrador,
            Observaciones  = Trim(observaciones),
        };
        g.SetCreated(createdBy);
        return g;
    }

    public void Validar(Guid userId)
    {
        if (Estado != EstadoGasto.Borrador)
            throw new InvalidOperationException("Solo se puede validar un gasto en borrador.");

        Estado       = EstadoGasto.Validado;
        ValidadoPor  = userId;
        ValidadoEn   = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Aprobar(Guid userId, Guid asientoContableId)
    {
        if (Estado != EstadoGasto.Validado)
            throw new InvalidOperationException("Solo se puede aprobar un gasto validado.");

        Estado            = EstadoGasto.Aprobado;
        AprobadoPor       = userId;
        AprobadoEn        = DateTime.UtcNow;
        AsientoContableId = asientoContableId;
        SetUpdated(userId);
    }

    /// <param name="tipoNota"><c>CREDITO</c> acumula y valida tope; <c>DEBITO</c> no modifica el acumulado de créditos.</param>
    public void RegistrarNotaProveedorAplicada(string tipoNota, decimal montoTotalNota, Guid userId)
    {
        if (montoTotalNota <= 0)
            throw new ArgumentException("El monto de la nota debe ser mayor a cero.", nameof(montoTotalNota));
        if (Estado != EstadoGasto.Aprobado)
            throw new InvalidOperationException("Solo se pueden aplicar notas de proveedor a un gasto Aprobado.");

        var tn = (tipoNota ?? string.Empty).Trim().ToUpperInvariant();
        if (tn == "CREDITO")
        {
            var suma = TotalNotasProveedorAplicado + montoTotalNota;
            if (suma > Total + ToleranciaTotal)
                throw new InvalidOperationException(
                    $"La suma de notas de crédito aplicadas ({suma:F2}) supera el total del gasto ({Total:F2}).");

            TotalNotasProveedorAplicado = suma;
        }
        else if (tn != "DEBITO")
            throw new ArgumentException("TipoNota debe ser CREDITO o DEBITO.", nameof(tipoNota));

        SetUpdated(userId);
    }

    public void Rechazar(Guid userId, string motivo)
    {
        if (Estado == EstadoGasto.Aprobado)
            throw new InvalidOperationException("No se puede rechazar un gasto ya aprobado.");
        if (Estado == EstadoGasto.Rechazado)
            throw new InvalidOperationException("El gasto ya está rechazado.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivo));

        Estado        = EstadoGasto.Rechazado;
        RechazadoPor  = userId;
        RechazadoEn   = DateTime.UtcNow;
        MotivoRechazo = motivo.Trim();
        SetUpdated(userId);
    }

    private static void ValidarMontos(decimal subtotal, decimal impuesto, decimal total)
    {
        if (subtotal < 0 || impuesto < 0 || total < 0)
            throw new ArgumentException("Los importes no pueden ser negativos.");

        var calculado = subtotal + impuesto;
        if (Math.Abs(calculado - total) > ToleranciaTotal)
            throw new ArgumentException(
                $"Subtotal + impuesto ({calculado:F2}) no coincide con Total ({total:F2}).");
    }

    private static void ValidarClaveAcceso(string claveAcceso)
    {
        if (string.IsNullOrWhiteSpace(claveAcceso))
            throw new ArgumentException("La clave de acceso es obligatoria para gastos con XML.", nameof(claveAcceso));
        var c = claveAcceso.Trim();
        if (c.Length != ClaveAccesoLen || !c.All(char.IsDigit))
            throw new ArgumentException("La clave de acceso debe tener 49 dígitos numéricos.");
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
