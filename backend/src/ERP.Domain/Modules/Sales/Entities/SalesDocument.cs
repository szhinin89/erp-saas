using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesDocument : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public const int EstabMaxLen         = 3;
    public const int EmPointMaxLen       = 3;
    public const int SequentialMaxLen    = 9;
    public const int AccessKeyMaxLen     = 49;
    public const int StatusMaxLen        = 30;
    public const int PaymentMethodMaxLen = 5;
    public const int NotesMaxLen         = 1000;

    private readonly List<SalesDetail> _lines = new();
    private readonly List<SalesPayment> _payments = new();

    public Guid?              CompanyId             { get; private set; }
    public Guid?              BranchId              { get; private set; }
    public Guid?              CustomerId            { get; private set; }
    public Guid?              WarehouseId           { get; private set; }
    public Guid?              SalespersonId         { get; private set; }
    public SalesDocumentType  DocType               { get; private set; } = SalesDocumentType.Invoice;
    public string?            DocNumber             { get; private set; }
    public string?            EstabCode             { get; private set; }
    public string?            EmPointCode           { get; private set; }
    public string?            Sequential            { get; private set; }
    public string?            AccessKey             { get; private set; }
    public DateTime           IssueDate             { get; private set; }
    public DateTime?          DueDate               { get; private set; }
    public DateTime?          DeliveryDate          { get; private set; }
    public Guid?              ReferenceDocumentId   { get; private set; }
    public string?            Reason                { get; private set; }
    public decimal            Subtotal              { get; private set; }
    public decimal            VatTotal              { get; private set; }
    public decimal            TotalDiscount         { get; private set; }
    public decimal            Total                 { get; private set; }
    public decimal            TotalNotesApplied     { get; private set; }
    public string             PaymentMethodCode     { get; private set; } = "01";
    public short              PaymentDays           { get; private set; }
    public string             Currency              { get; private set; } = "USD";
    public string             Status                { get; private set; } = "Borrador";
    public string?            Notes                 { get; private set; }
    public string?            RemittanceKey         { get; private set; }
    public string?            GuideDocNum           { get; private set; }
    public Guid?              JournalEntryId        { get; private set; }

    public Customer?              Cliente     { get; private set; }
    public Warehouse?             Warehouse   { get; private set; }
    public SalesDocument?         Reference   { get; private set; }
    public SalesElectronicDoc?    Electronic  { get; private set; }
    public IReadOnlyList<SalesDetail>   Lines     => _lines.AsReadOnly();
    public IReadOnlyList<SalesPayment>  Payments  => _payments.AsReadOnly();

    private SalesDocument() { }

    public static SalesDocument Rehydrate() => new();

    public static SalesDocument CreateInvoice(
        Guid subscriberId,
        Guid branchId,
        Guid customerId,
        Guid warehouseId,
        string estabCode,
        string emPointCode,
        string sequential,
        string accessKey,
        DateTime issueDate,
        decimal subtotal,
        decimal vatTotal,
        decimal total,
        decimal totalDiscount,
        string paymentMethodCode,
        short paymentDays,
        string? notes,
        Guid createdBy,
        Guid? companyId = null)
    {
        var doc = new SalesDocument
        {
            Id                = Guid.NewGuid(),
            SubscriberId          = subscriberId,
            CompanyId         = companyId,
            BranchId          = branchId,
            CustomerId        = customerId,
            WarehouseId       = warehouseId,
            DocType           = SalesDocumentType.Invoice,
            EstabCode         = estabCode.Trim(),
            EmPointCode       = emPointCode.Trim(),
            Sequential        = sequential.Trim(),
            AccessKey         = accessKey.Trim(),
            IssueDate         = issueDate,
            Subtotal          = subtotal,
            VatTotal          = vatTotal,
            Total             = total,
            TotalDiscount     = totalDiscount,
            PaymentMethodCode = string.IsNullOrWhiteSpace(paymentMethodCode) ? "01" : paymentMethodCode.Trim(),
            PaymentDays       = paymentDays < 0 ? (short)0 : paymentDays,
            Notes             = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status            = "Borrador",
        };
        doc.SetCreated(createdBy);
        return doc;
    }

    public void Validate(Guid userId)
    {
        if (Status != "Borrador")
            throw new InvalidOperationException($"Solo documentos en Borrador pueden validarse (actual: {Status}).");
        Status = "Validado";
        SetUpdated(userId);
    }

    public void Authorize(
        Guid userId,
        string authNumber,
        DateTime authDate,
        string? xmlSignedPath,
        string? xmlAuthPath,
        Guid journalEntryId,
        IReadOnlyList<SalesBillAuthorizedStockLine>? stockLines = null)
    {
        if (Status != "Validado")
            throw new InvalidOperationException($"Solo documentos Validados pueden autorizarse (actual: {Status}).");
        Status         = "Autorizado";
        JournalEntryId = journalEntryId;
        EnsureElectronic();
        Electronic!.SetAuthorization(authNumber, authDate, xmlSignedPath, xmlAuthPath);
        SetUpdated(userId);

        if (DocType == SalesDocumentType.Invoice
            && WarehouseId.HasValue
            && stockLines is { Count: > 0 })
        {
            var number = $"{EstabCode}-{EmPointCode}-{Sequential}";
            RaiseDomainEvent(new SalesBillAuthorizedEvent(Id, SubscriberId, userId, WarehouseId.Value, CompanyId ?? Guid.Empty, number, stockLines));
        }
    }

    public void Reject(Guid userId, string errorMessage)
    {
        Status = "Rechazado";
        EnsureElectronic();
        Electronic!.SetError(errorMessage);
        SetUpdated(userId);
    }

    public void MarkSendError(Guid userId, string errorMessage)
    {
        Status = "ErrorEnvio";
        EnsureElectronic();
        Electronic!.SetError(errorMessage);
        SetUpdated(userId);
    }

    public void PrepareRetry(Guid userId)
    {
        if (Status is not ("ErrorEnvio" or "Rechazado"))
            throw new InvalidOperationException($"Solo documentos en ErrorEnvio o Rechazado pueden reintentarse (actual: {Status}).");
        Status = "Validado";
        Electronic?.ClearError();
        SetUpdated(userId);
    }

    public void Void(Guid userId)
    {
        if (Status is "Autorizado" or "Anulado")
            throw new InvalidOperationException($"No se puede anular un documento en estado {Status}.");
        Status = "Anulado";
        SetUpdated(userId);
    }

    public void AddLine(SalesDetail line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        RecalcTotals();
    }

    public void AddPayment(SalesPayment payment) => _payments.Add(payment);

    public void AttachElectronic(SalesElectronicDoc electronic) => Electronic = electronic;

    private void EnsureElectronic()
    {
        if (Electronic is not null) return;
        Electronic = SalesElectronicDoc.CreateShell(Id, SubscriberId, CompanyId);
    }

    private void RecalcTotals()
    {
        TotalDiscount = _lines.Sum(d => d.DiscountAmount);
        Subtotal      = _lines.Sum(d => d.Subtotal);
        VatTotal      = _lines.Sum(d => d.VatAmount);
        Total         = _lines.Sum(d => d.Total);
    }
}
