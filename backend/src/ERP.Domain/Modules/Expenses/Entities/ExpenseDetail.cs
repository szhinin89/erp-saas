using ERP.Domain.Common;

namespace ERP.Domain.Modules.Expenses.Entities;

/// <summary>
/// Detalle del gasto. Comparte scope de suscriptor con <see cref="ExpenseInvoice"/>.
/// </summary>
public class ExpenseDetail : ISubscriberScopedEntity
{
    public Guid Id { get; set; }
    public Guid SubscriberId { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public short SortOrder { get; set; }

    public ExpenseInvoice Expense { get; set; } = null!;
}
