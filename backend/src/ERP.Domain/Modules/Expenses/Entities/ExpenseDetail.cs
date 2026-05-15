namespace ERP.Domain.Modules.Expenses.Entities;

/// <summary>
/// Detalle del gasto. Tabla nueva para soportar patrón maestro-detalle en gastos.
/// </summary>
public class ExpenseDetail
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public short SortOrder { get; set; }

    public GastoFactura Expense { get; set; } = null!;
}
