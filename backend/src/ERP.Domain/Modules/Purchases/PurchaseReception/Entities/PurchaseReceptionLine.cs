using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

/// <summary>
/// Línea de detalle de un <see cref="PurchaseReceptionDocument"/>. Fase 2 no la carga desde el
/// TXT (que solo trae totales de cabecera) — queda modelada para cuando exista una fuente con
/// detalle de línea (XML del comprobante). <see cref="ItemId"/>/<see cref="MatchStatus"/> quedan
/// reservados para la futura conciliación de productos — sin lógica de matching todavía.
/// </summary>
public sealed class PurchaseReceptionLine : IMustHaveTenant
{
    public const int SupplierCodeMaxLen = 50;
    public const int DescriptionMaxLen  = 300;
    public const int MatchStatusMaxLen  = 30;

    public Guid Id                          { get; private set; }
    public Guid TenantId                    { get; private set; }
    public Guid PurchaseReceptionDocumentId { get; private set; }

    public string? SupplierCode { get; private set; }
    public string  Description  { get; private set; } = null!;
    public decimal Quantity     { get; private set; }
    public decimal UnitPrice    { get; private set; }

    /// <summary>Ítem del ERP ya conciliado con esta línea — null hasta que exista un matcher.</summary>
    public Guid?   ItemId      { get; private set; }
    /// <summary>Marcador libre para la futura fase de conciliación — sin vocabulario fijo todavía.</summary>
    public string? MatchStatus { get; private set; }

    private PurchaseReceptionLine() { }

    public static PurchaseReceptionLine Create(
        Guid documentId, Guid tenantId, string description, decimal quantity, decimal unitPrice,
        string? supplierCode = null, Guid? itemId = null, string? matchStatus = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("El documento de recepción es obligatorio.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción es obligatoria.", nameof(description));
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(unitPrice));

        return new PurchaseReceptionLine
        {
            Id                          = Guid.NewGuid(),
            TenantId                    = tenantId,
            PurchaseReceptionDocumentId = documentId,
            SupplierCode                = supplierCode?.Trim(),
            Description                 = description.Trim(),
            Quantity                    = quantity,
            UnitPrice                   = unitPrice,
            ItemId                      = itemId,
            MatchStatus                 = matchStatus?.Trim(),
        };
    }
}
