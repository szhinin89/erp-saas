using ERP.Domain.Common;
using ERP.Domain.Modules.Fiscal.Events;

namespace ERP.Domain.Modules.Fiscal.Entities;

public sealed class Invoice : SnowflakeAuditableEntity
{
    public const int NumberMaxLen = 20;
    public const int EstabMaxLen = 3;
    public const int EmPointMaxLen = 3;
    public const int SequentialMaxLen = 9;
    public const int AccessKeyMaxLen = 49;
    public const int StatusMaxLen = 20;
    public const int PaymentMethodMaxLen = 2;
    public const int NotesMaxLen = 500;
    public const int BuyerIdTypeMaxLen = 2;
    public const int BuyerIdNumberMaxLen = 20;
    public const int BuyerNameMaxLen = 300;

    public static class Statuses
    {
        public const string Draft = "Borrador";
        public const string Validated = "Validado";
        public const string Authorized = "Autorizado";
        public const string Rejected = "Rechazado";
        public const string SendError = "ErrorEnvio";
        public const string Voided = "Anulado";
    }

    private readonly List<InvoiceDetail> _lines = new();
    private readonly List<InvoiceStatusHistory> _statusHistory = new();

    public Guid BranchId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string DocType { get; private set; } = "01";
    public string EstabCode { get; private set; } = null!;
    public string EmPointCode { get; private set; } = null!;
    public string Sequential { get; private set; } = null!;
    public string AccessKey { get; private set; } = null!;
    public DateTime IssueDate { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }
    public decimal TotalDiscount { get; private set; }
    public string PaymentMethodCode { get; private set; } = "01";
    public short PaymentTermDays { get; private set; }
    public string? Notes { get; private set; }
    public string Status { get; private set; } = Statuses.Draft;
    public string BuyerIdType { get; private set; } = null!;
    public string BuyerIdNumber { get; private set; } = null!;
    public string BuyerNameSnapshot { get; private set; } = null!;
    public string? BuyerAddressSnapshot { get; private set; }
    public Guid? JournalEntryId { get; private set; }

    public InvoiceElectronic? Electronic { get; private set; }

    public IReadOnlyList<InvoiceDetail> Lines => _lines.AsReadOnly();
    public IReadOnlyList<InvoiceStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public string InvoiceNumber => $"{EstabCode}-{EmPointCode}-{Sequential}";

    private Invoice() { }

    public static Invoice Create(
        long id,
        Guid publicId,
        Guid subscriberId,
        Guid branchId,
        Guid businessPartnerId,
        Guid warehouseId,
        string docType,
        string estabCode,
        string emPointCode,
        string sequential,
        string accessKey,
        DateTime issueDate,
        string buyerIdType,
        string buyerIdNumber,
        string buyerNameSnapshot,
        string? buyerAddressSnapshot,
        string paymentMethodCode,
        short paymentTermDays,
        string? notes,
        Guid createdBy)
    {
        var invoice = new Invoice
        {
            Id = id,
            PublicId = publicId,
            SubscriberId = subscriberId,
            CompanyId = branchId,
            BranchId = branchId,
            BusinessPartnerId = businessPartnerId,
            WarehouseId = warehouseId,
            DocType = string.IsNullOrWhiteSpace(docType) ? "01" : docType.Trim(),
            EstabCode = estabCode.Trim(),
            EmPointCode = emPointCode.Trim(),
            Sequential = sequential.Trim(),
            AccessKey = accessKey.Trim(),
            IssueDate = issueDate,
            BuyerIdType = buyerIdType.Trim(),
            BuyerIdNumber = buyerIdNumber.Trim(),
            BuyerNameSnapshot = buyerNameSnapshot.Trim(),
            BuyerAddressSnapshot = string.IsNullOrWhiteSpace(buyerAddressSnapshot) ? null : buyerAddressSnapshot.Trim(),
            PaymentMethodCode = string.IsNullOrWhiteSpace(paymentMethodCode) ? "01" : paymentMethodCode.Trim(),
            PaymentTermDays = paymentTermDays < 0 ? (short)0 : paymentTermDays,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = Statuses.Draft,
        };
        invoice.SetCreated(createdBy);
        invoice.RecordStatus(null, Statuses.Draft, null, createdBy);
        invoice.RaiseDomainEvent(new InvoiceCreatedEvent(invoice.Id, invoice.PublicId, subscriberId, branchId, createdBy));
        return invoice;
    }

    public void AttachElectronic(InvoiceElectronic electronic) => Electronic = electronic;

    public void AddLine(InvoiceDetail line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        RecalcTotals();
    }

    public void RecalcTotals()
    {
        TotalDiscount = _lines.Sum(d => d.DiscountAmount);
        Subtotal = _lines.Sum(d => d.LineSubtotal);
        TaxTotal = _lines.Sum(d => d.LineTax);
        Total = _lines.Sum(d => d.LineTotal);
    }

    public void Validate(Guid userId) =>
        Transition(Statuses.Validated, userId, null, s => s == Statuses.Draft);

    public void Authorize(
        Guid userId,
        string authNumber,
        DateTime authDate,
        Guid journalEntryId,
        IReadOnlyList<InvoiceAuthorizedStockLine>? stockLines = null)
    {
        if (Status != Statuses.Validated)
            throw new InvalidOperationException($"Solo facturas Validadas pueden autorizarse (actual: {Status}).");

        JournalEntryId = journalEntryId;
        Transition(Statuses.Authorized, userId, null, s => s == Statuses.Validated);

        var lines = stockLines ?? Array.Empty<InvoiceAuthorizedStockLine>();
        if (lines.Count > 0)
        {
            RaiseDomainEvent(new InvoiceAuthorizedEvent(
                Id,
                PublicId,
                SubscriberId,
                userId,
                WarehouseId,
                BranchId,
                InvoiceNumber,
                lines));
        }
    }

    public void Reject(Guid userId, string errorMessage) =>
        Transition(Statuses.Rejected, userId, errorMessage, s => s == Statuses.Validated);

    public void MarkSendError(Guid userId, string errorMessage) =>
        Transition(Statuses.SendError, userId, errorMessage, s => s == Statuses.Validated);

    public void PrepareRetry(Guid userId)
    {
        if (Status is not (Statuses.SendError or Statuses.Rejected))
            throw new InvalidOperationException($"Solo facturas en ErrorEnvio o Rechazado pueden reintentarse (actual: {Status}).");
        Transition(Statuses.Validated, userId, null, _ => true);
    }

    public void Void(Guid userId)
    {
        if (Status is Statuses.Authorized or Statuses.Voided)
            throw new InvalidOperationException($"No se puede anular una factura en estado {Status}.");
        Transition(Statuses.Voided, userId, null, s => s is not (Statuses.Authorized or Statuses.Voided));
    }

    public void AssignSnowflakeChildIds(ISnowflakeIdGenerator idGenerator)
    {
        foreach (var line in _lines)
        {
            if (line.Id == 0)
                line.AssignId(idGenerator.NextId());
        }

        foreach (var history in _statusHistory)
        {
            if (history.Id == 0)
                history.AssignId(idGenerator.NextId());
        }

        Electronic?.AssignIdIfNeeded(idGenerator);
    }

    private void Transition(string toStatus, Guid userId, string? reason, Func<string, bool> canTransition)
    {
        if (!canTransition(Status))
            throw new InvalidOperationException($"Transición inválida de {Status} a {toStatus}.");
        var from = Status;
        Status = toStatus;
        SetUpdated(userId);
        RecordStatus(from, toStatus, reason, userId);
    }

    private void RecordStatus(string? from, string to, string? reason, Guid userId)
    {
        _statusHistory.Add(InvoiceStatusHistory.Create(
            SubscriberId,
            Id,
            DateOnly.FromDateTime(IssueDate),
            from,
            to,
            reason,
            userId));
    }
}

public sealed record InvoiceAuthorizedStockLine(Guid ProductId, decimal Quantity);
