namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesPayment
{
    public Guid     Id                { get; private set; } = Guid.NewGuid();
    public Guid     TenantId          { get; private set; }
    public Guid     SalesDocumentId   { get; private set; }
    public string   PaymentMethod     { get; private set; } = "01";
    public decimal? Amount            { get; private set; }
    public int?     PaymentTerm       { get; private set; }
    public string?  Bank              { get; private set; }
    public string?  AccountNumber     { get; private set; }

    private SalesPayment() { }

    public static SalesPayment Create(
        Guid tenantId,
        Guid salesDocumentId,
        string paymentMethod,
        decimal? amount = null,
        int? paymentTerm = null)
    {
        return new SalesPayment
        {
            TenantId        = tenantId,
            SalesDocumentId = salesDocumentId,
            PaymentMethod   = paymentMethod,
            Amount          = amount,
            PaymentTerm     = paymentTerm,
        };
    }
}
