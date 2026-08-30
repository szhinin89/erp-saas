namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuándo impacta en inventario un documento de este tipo.</summary>
public enum InventoryImpactMode
{
    None = 0,
    OnConfirmation = 1,
    OnAuthorization = 2,
}
