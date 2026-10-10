namespace ERP.Domain.Modules.InitialLoad.Enums;

/// <summary>
/// Tipo de dato maestro que un <c>ImportBatch</c> carga. Solo <see cref="Customers"/> tiene
/// un <c>IImportProcessor</c> registrado en esta entrega — los demás valores existen para que
/// futuros importadores (Suppliers/Items/Prices/InitialStock) se agreguen sin renumerar ni
/// tocar el motor genérico. Nunca reordenar/renumerar valores existentes (persistidos en BD).
/// </summary>
public enum ImportType
{
    Customers = 1,
    Suppliers = 2,
    Items = 3,
    Prices = 4,
    InitialStock = 5,

    /// <summary>IL-5 — saldos pendientes de CxC al corte (nunca ventas históricas).</summary>
    InitialReceivables = 6,

    /// <summary>IL-6 — saldos pendientes de CxP al corte (nunca compras/gastos históricos).</summary>
    InitialPayables = 7,
}
