namespace ERP.Domain.Kernel.Permissions;

public static class FinancePermissions
{
    public const string View = "finance.view";
    public const string Create = "finance.create";
    public const string Update = "finance.update";
    public const string Delete = "finance.delete";
    public const string ReverseCollection = "finance.collections.reverse";

    // CUSTOMER-ADVANCES-V1 (ADR-052 §11) — independientes por operación. Las consultas usan View.
    // Asignables solo cuando su pantalla los relacione en la navegación.
    public const string CustomerAdvancesCreate = "finance.customer-advances.create";
    public const string CustomerAdvancesApply = "finance.customer-advances.apply";
    public const string CustomerAdvancesReverse = "finance.customer-advances.reverse";
    public const string CustomerAdvancesRefund = "finance.customer-advances.refund";
}
