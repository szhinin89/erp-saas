namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>Resultado de comparar una factura recibida del SRI contra los datos ya existentes en el ERP.</summary>
public enum PurchaseReceptionStatus
{
    /// <summary>El proveedor no existe en el ERP.</summary>
    NewSupplier = 0,

    /// <summary>El proveedor existe pero la compra todavía no fue registrada.</summary>
    Pending = 1,

    /// <summary>La compra ya existe en el ERP.</summary>
    Imported = 2,
}
