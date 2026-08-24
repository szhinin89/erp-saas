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
}
