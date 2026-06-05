using ERP.Domain.Common;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Domain.Modules.Expenses.Entities;

/// <summary>Cabecera unificada de gastos (tabla expense_document).</summary>
public sealed class ExpenseDocument : MasterEntity, ICompanyOperationalEntity
{
    public Guid               CompanyId         { get; private set; }
    public Guid?              BusinessPartnerId { get; private set; }
    public ExpenseDocumentType DocType          { get; private set; } = ExpenseDocumentType.Invoice;
    public string?            DocNumber         { get; private set; }
    public string?            AccessKey         { get; private set; }
    public DateTime           IssueDate         { get; private set; }
    public string             Concept           { get; private set; } = null!;
    public string             Category          { get; private set; } = null!;
    public decimal            Subtotal          { get; private set; }
    public decimal            TaxTotal          { get; private set; }
    public decimal            Total             { get; private set; }
    public decimal            TotalNotesApplied { get; private set; }
    public ExpenseStatus      Status            { get; private set; } = ExpenseStatus.Draft;
    public string?            Notes             { get; private set; }
    public string?            XmlPath           { get; private set; }
    public Guid?              JournalEntryId    { get; private set; }
    public Guid?              ValidatedBy       { get; private set; }
    public DateTime?          ValidatedAt       { get; private set; }
    public Guid?              ApprovedBy        { get; private set; }
    public DateTime?          ApprovedAt        { get; private set; }
    public Guid?              RejectedBy        { get; private set; }
    public DateTime?          RejectedAt        { get; private set; }
    public string?            RejectionReason   { get; private set; }

    public ICollection<ExpenseDetail> Details { get; set; } = [];

    private ExpenseDocument() { }

    public void SetBusinessPartner(Guid? businessPartnerId) => BusinessPartnerId = businessPartnerId;

    /// <summary>Rehidratación desde capa de persistencia / mapper.</summary>
    public static ExpenseDocument Rehydrate() => new();
}
