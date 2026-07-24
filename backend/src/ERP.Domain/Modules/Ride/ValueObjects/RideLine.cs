namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>Línea de detalle del comprobante (<c>detalle</c> en el XML autorizado), con sus impuestos.</summary>
public sealed record RideLine
{
    public string Code { get; }
    public string Description { get; }
    public decimal Quantity { get; }
    public decimal UnitPrice { get; }
    public decimal Discount { get; }
    public decimal Subtotal { get; }
    public IReadOnlyList<RideTaxSummary> Taxes { get; }

    private RideLine(
        string code, string description, decimal quantity, decimal unitPrice,
        decimal discount, decimal subtotal, IReadOnlyList<RideTaxSummary> taxes)
    {
        Code = code;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Discount = discount;
        Subtotal = subtotal;
        Taxes = taxes;
    }

    public static RideLine Create(
        string code, string description, decimal quantity, decimal unitPrice,
        decimal discount, decimal subtotal, IReadOnlyList<RideTaxSummary> taxes)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de la línea es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripción de la línea es obligatoria.", nameof(description));
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(unitPrice));
        if (discount < 0)
            throw new ArgumentException("El descuento no puede ser negativo.", nameof(discount));
        if (subtotal < 0)
            throw new ArgumentException("El subtotal de la línea no puede ser negativo.", nameof(subtotal));
        ArgumentNullException.ThrowIfNull(taxes);

        return new RideLine(code.Trim(), description.Trim(), quantity, unitPrice, discount, subtotal, taxes);
    }
}
