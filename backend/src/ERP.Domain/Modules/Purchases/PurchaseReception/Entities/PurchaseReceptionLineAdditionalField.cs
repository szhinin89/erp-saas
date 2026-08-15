using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

/// <summary>
/// PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — snapshot fiel de un <c>&lt;detAdicional&gt;</c> del XML,
/// persistido junto con la <see cref="PurchaseReceptionLine"/> de origen. Mismo motivo que
/// <see cref="PurchaseReceptionLineTax"/>: el flujo de Recepción Electrónica es de 2 pasos (XML →
/// línea de recepción persistida → más tarde "Crear compra" la lee) — sin esto, un dato como lote,
/// serie o fecha de caducidad declarado como <c>detAdicional</c> se perdería en el paso intermedio.
/// Solo guarda nombre/valor/posición crudos, tal como el proveedor los declaró — nunca se normaliza,
/// traduce ni convierte a una entidad operativa (Lote, Serie, etc.).
/// </summary>
public sealed class PurchaseReceptionLineAdditionalField : IMustHaveTenant
{
    public const int NameMaxLen = 300;
    public const int ValueMaxLen = 300;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseReceptionLineId { get; private set; }

    /// <summary>Atributo <c>&lt;detAdicional&gt;/nombre</c> tal como lo declaró el proveedor — nunca normalizado.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Atributo <c>&lt;detAdicional&gt;/valor</c> tal como lo declaró el proveedor — nunca normalizado.</summary>
    public string Value { get; private set; } = null!;

    /// <summary>Orden de aparición dentro de <c>&lt;detallesAdicionales&gt;</c> — permite mostrarlos en el mismo orden del XML.</summary>
    public int SortOrder { get; private set; }

    private PurchaseReceptionLineAdditionalField() { }

    public static PurchaseReceptionLineAdditionalField Create(
        Guid purchaseReceptionLineId,
        Guid tenantId,
        string name,
        string value,
        int sortOrder
    )
    {
        if (purchaseReceptionLineId == Guid.Empty)
            throw new ArgumentException(
                "La línea de recepción es obligatoria.",
                nameof(purchaseReceptionLineId)
            );
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "El dato adicional debe tener nombre o valor.",
                nameof(name)
            );

        return new PurchaseReceptionLineAdditionalField
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseReceptionLineId = purchaseReceptionLineId,
            Name = name?.Trim() ?? string.Empty,
            Value = value?.Trim() ?? string.Empty,
            SortOrder = sortOrder,
        };
    }
}
