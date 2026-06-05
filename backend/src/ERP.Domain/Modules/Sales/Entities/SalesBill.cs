using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesBill : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public const int DocTypeMaxLen       = 10;
    public const int EstabMaxLen         = 3;
    public const int EmPointMaxLen       = 3;
    public const int SequentialMaxLen    = 9;
    public const int AccessKeyMaxLen     = 49;
    public const int StatusMaxLen        = 20;
    public const int XmlPathMaxLen       = 500;
    public const int ErrorMaxLen         = 1000;
    public const int PaymentMethodMaxLen = 2;
    public const int NotesMaxLen         = 500;

    private readonly List<SalesBillLine> _lines = new();

    public Guid      BranchId            { get; private set; }
    public Guid      BusinessPartnerId  { get; private set; }
    public Guid      WarehouseId         { get; private set; }
    public string    DocType           { get; private set; } = "01";
    public string    EstabCode         { get; private set; } = null!;
    public string    EmPointCode       { get; private set; } = null!;
    public string    Sequential        { get; private set; } = null!;
    public string    AccessKey         { get; private set; } = null!;
    public DateTime  IssueDate         { get; private set; }
    public decimal   Subtotal          { get; private set; }
    public decimal   VatTotal          { get; private set; }
    public decimal   Total             { get; private set; }
    public decimal   TotalDiscount     { get; private set; }
    /// <summary>CÃ³digo SRI forma de pago: "01"=efectivo, "02"=cheque, "03"=crÃ©dito, "04"=transferencia...</summary>
    public string    PaymentMethodCode { get; private set; } = "01";
    /// <summary>Plazo en dÃ­as (0 = contado).</summary>
    public short     PaymentDays       { get; private set; }
    /// <summary>notes libres â€” se emiten como &lt;campoAdicional&gt; en el XML SRI.</summary>
    public string?   Notes             { get; private set; }
    public string    Status            { get; private set; } = "Borrador";
    public string?   XmlSignedPath     { get; private set; }
    public string?   XmlAuthPath       { get; private set; }
    public string?   AuthNumber        { get; private set; }
    public DateTime? AuthDate          { get; private set; }
    public string?   ErrorMessage      { get; private set; }
    public Guid?     JournalEntryId    { get; private set; }

    public Warehouse Warehouse { get; private set; } = null!;
    public IReadOnlyList<SalesBillLine> Lines => _lines.AsReadOnly();

    private SalesBill() { }

    public static SalesBill Create(
        Guid      subscriberId,
        Guid      branchId,
        Guid      businessPartnerId,
        Guid      warehouseId,
        string    docType,
        string    estabCode,
        string    emPointCode,
        string    sequential,
        string    accessKey,
        DateTime  issueDate,
        decimal   subtotal,
        decimal   vatTotal,
        decimal   total,
        decimal   totalDiscount,
        string    paymentMethodCode,
        short     paymentDays,
        string?   notes,
        string?   xmlSignedPath,
        string?   xmlAuthPath,
        string?   authNumber,
        DateTime? authDate,
        string?   errorMessage,
        Guid      createdBy,
        Guid companyId = default)
    {
        var b = new SalesBill
        {
            Id                = Guid.NewGuid(),
            SubscriberId          = subscriberId,
            CompanyId = companyId,
            BranchId            = branchId,
            BusinessPartnerId  = businessPartnerId,
            WarehouseId         = warehouseId,
            DocType           = string.IsNullOrWhiteSpace(docType) ? "01" : docType.Trim(),
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
            XmlSignedPath     = string.IsNullOrWhiteSpace(xmlSignedPath) ? null : xmlSignedPath.Trim(),
            XmlAuthPath       = string.IsNullOrWhiteSpace(xmlAuthPath) ? null : xmlAuthPath.Trim(),
            AuthNumber        = string.IsNullOrWhiteSpace(authNumber) ? null : authNumber.Trim(),
            AuthDate          = authDate,
            ErrorMessage      = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim(),
        };
        b.SetCreated(createdBy);
        return b;
    }

    public void Validate(Guid userId)
    {
        if (Status != "Borrador")
            throw new InvalidOperationException($"Solo facturas en Borrador pueden validarse (actual: {Status}).");
        Status = "Validado";
        SetUpdated(userId);
    }

    public void Authorize(
        Guid     userId,
        string   authNumber,
        DateTime authDate,
        string?  xmlSignedPath,
        string?  xmlAuthPath,
        Guid     journalEntryId,
        IReadOnlyList<SalesBillAuthorizedStockLine>? stockLines = null)
    {
        if (Status != "Validado")
            throw new InvalidOperationException($"Solo facturas Validadas pueden autorizarse (actual: {Status}).");
        Status         = "Autorizado";
        AuthNumber     = authNumber;
        AuthDate       = authDate;
        XmlSignedPath  = string.IsNullOrWhiteSpace(xmlSignedPath) ? null : xmlSignedPath;
        XmlAuthPath    = string.IsNullOrWhiteSpace(xmlAuthPath) ? null : xmlAuthPath;
        JournalEntryId = journalEntryId;
        SetUpdated(userId);

        var lines = stockLines ?? Array.Empty<SalesBillAuthorizedStockLine>();
        if (lines.Count > 0)
        {
            var number = $"{EstabCode}-{EmPointCode}-{Sequential}";
            RaiseDomainEvent(new SalesBillAuthorizedEvent(Id, SubscriberId, userId, WarehouseId, CompanyId, number, lines));
        }
    }

    public void Reject(Guid userId, string errorMessage)
    {
        Status       = "Rechazado";
        ErrorMessage = errorMessage?.Trim();
        SetUpdated(userId);
    }

    public void MarkSendError(Guid userId, string errorMessage)
    {
        Status       = "ErrorEnvio";
        ErrorMessage = errorMessage?.Trim();
        SetUpdated(userId);
    }

    public void PrepareRetry(Guid userId)
    {
        if (Status is not ("ErrorEnvio" or "Rechazado"))
            throw new InvalidOperationException($"Solo facturas en ErrorEnvio o Rechazado pueden reintentarse (actual: {Status}).");
        Status       = "Validado";
        ErrorMessage = null;
        SetUpdated(userId);
    }

    public void Void(Guid userId)
    {
        if (Status is "Autorizado" or "Anulado")
            throw new InvalidOperationException($"No se puede anular una salesBill en estado {Status}.");
        Status = "Anulado";
        SetUpdated(userId);
    }

    public void AddLine(SalesBillLine line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
        RecalcTotals();
    }

    private void RecalcTotals()
    {
        TotalDiscount = _lines.Sum(d => d.DiscountAmount);
        Subtotal      = _lines.Sum(d => d.Subtotal);
        VatTotal      = _lines.Sum(d => d.VatTotal);
        Total         = _lines.Sum(d => d.Total);
    }
}
