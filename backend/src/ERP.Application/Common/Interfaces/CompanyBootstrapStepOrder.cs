namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Fuente única de los valores de <see cref="ICompanyBootstrapStep.Order"/>. Ningún step debe
/// declarar su orden como literal — siempre referenciar una constante de aquí.
///
/// Convención: incrementos de 10 para poder insertar un step nuevo entre dos existentes sin
/// renumerar los demás (p. ej. un futuro step de Compras entre Inventario y Ventas podría usar 35).
/// </summary>
public static class CompanyBootstrapStepOrder
{
    /// <summary>Sucursal Principal, Bodega Principal, Establecimiento, Punto de Emisión.</summary>
    public const int Organization = 10;

    /// <summary>Numeraciones (DocumentSequence). Depende de Organization (punto de emisión).</summary>
    public const int ElectronicDocuments = 20;

    /// <summary>Tipos de ítem por defecto.</summary>
    public const int Inventory = 30;

    /// <summary>Formas de pago, Consumidor Final, Lista de Precios, Condición de Pago.</summary>
    public const int Sales = 40;

    /// <summary>Caja Principal. Depende de Organization (sucursal) y ElectronicDocuments (punto de emisión por defecto).</summary>
    public const int Caja = 45;

    /// <summary>Perfiles de acceso por defecto. Último — no depende de ningún otro step.</summary>
    public const int Access = 50;
}
