namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseElectronicDoc
{
    public Guid    PurchaseDocumentId     { get; private set; }
    public Guid    SubscriberId               { get; private set; }
    public Guid    CompanyId              { get; private set; }
    public string? AccessKey              { get; private set; }
    public string? XmlPath                { get; private set; }

    private PurchaseElectronicDoc() { }
}
