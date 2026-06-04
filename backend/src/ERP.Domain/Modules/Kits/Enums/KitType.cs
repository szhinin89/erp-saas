namespace ERP.Domain.Modules.Kits.Enums;

public enum KitType
{
    /// <summary>Componentes y cantidades fijas. Se desensambla al vender.</summary>
    Fixed    = 1,
    /// <summary>Cantidades ajustables en el punto de venta.</summary>
    Variable = 2,
    /// <summary>Agrupa ítems para precio conjunto. No desensambla stock.</summary>
    Bundle   = 3,
}
