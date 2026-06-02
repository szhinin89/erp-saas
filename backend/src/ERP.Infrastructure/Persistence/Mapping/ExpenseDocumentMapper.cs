using ERP.Domain.Modules.Expenses.Entities;
using ERP.Infrastructure.Persistence.Converters;

namespace ERP.Infrastructure.Persistence.Mapping;

public static class ExpenseDocumentMapper
{
    public static ExpenseDocument ToDocument(ExpenseInvoice invoice)
    {
        var doc = ExpenseDocument.Rehydrate();
        Set(doc, nameof(ExpenseDocument.Id), invoice.Id);
        Set(doc, nameof(ExpenseDocument.SubscriberId), invoice.SubscriberId);
        Set(doc, nameof(ExpenseDocument.BusinessPartnerId), invoice.BusinessPartnerId);
        Set(doc, nameof(ExpenseDocument.DocType), ExpenseDocumentTypeConversions.FromDb(invoice.Category));
        Set(doc, nameof(ExpenseDocument.DocNumber), invoice.InvoiceNumber);
        Set(doc, nameof(ExpenseDocument.AccessKey), invoice.AccessKey);
        Set(doc, nameof(ExpenseDocument.IssueDate), invoice.IssueDate);
        Set(doc, nameof(ExpenseDocument.Concept), invoice.Concept);
        Set(doc, nameof(ExpenseDocument.Category), invoice.Category);
        Set(doc, nameof(ExpenseDocument.Subtotal), invoice.Subtotal);
        Set(doc, nameof(ExpenseDocument.TaxTotal), invoice.TaxTotal);
        Set(doc, nameof(ExpenseDocument.Total), invoice.Total);
        Set(doc, nameof(ExpenseDocument.TotalNotesApplied), invoice.TotalNotesApplied);
        Set(doc, nameof(ExpenseDocument.Status), invoice.Status);
        Set(doc, nameof(ExpenseDocument.Notes), invoice.Notes);
        Set(doc, nameof(ExpenseDocument.XmlPath), invoice.XmlPath);
        Set(doc, nameof(ExpenseDocument.JournalEntryId), invoice.JournalEntryId);
        Set(doc, nameof(ExpenseDocument.ValidatedBy), invoice.ValidatedBy);
        Set(doc, nameof(ExpenseDocument.ValidatedAt), invoice.ValidatedAt);
        Set(doc, nameof(ExpenseDocument.ApprovedBy), invoice.ApprovedBy);
        Set(doc, nameof(ExpenseDocument.ApprovedAt), invoice.ApprovedAt);
        Set(doc, nameof(ExpenseDocument.RejectedBy), invoice.RejectedBy);
        Set(doc, nameof(ExpenseDocument.RejectedAt), invoice.RejectedAt);
        Set(doc, nameof(ExpenseDocument.RejectionReason), invoice.RejectionReason);
        Set(doc, nameof(ExpenseDocument.IsActive), invoice.IsActive);
        Set(doc, nameof(ExpenseDocument.CreatedAt), invoice.CreatedAt);
        Set(doc, nameof(ExpenseDocument.UpdatedAt), invoice.UpdatedAt);
        Set(doc, nameof(ExpenseDocument.CreatedBy), invoice.CreatedBy);
        Set(doc, nameof(ExpenseDocument.UpdatedBy), invoice.UpdatedBy);
        doc.Details = invoice.Details.ToList();
        foreach (var d in doc.Details)
            d.ExpenseId = invoice.Id;
        return doc;
    }

    public static ExpenseInvoice ToLegacyInvoice(ExpenseDocument doc)
    {
        ExpenseInvoice invoice;
        if (!string.IsNullOrWhiteSpace(doc.AccessKey) && !string.IsNullOrWhiteSpace(doc.XmlPath))
        {
            invoice = ExpenseInvoice.CreateFromXml(
                doc.SubscriberId,
                doc.BusinessPartnerId ?? Guid.Empty,
                doc.AccessKey!,
                doc.DocNumber,
                doc.IssueDate,
                doc.Concept,
                doc.Category,
                doc.Subtotal,
                doc.TaxTotal,
                doc.Total,
                doc.XmlPath!,
                doc.Notes,
                doc.CreatedBy);
        }
        else
        {
            invoice = ExpenseInvoice.CreateManual(
                doc.SubscriberId,
                doc.BusinessPartnerId,
                doc.IssueDate,
                doc.Concept,
                doc.Category,
                doc.Subtotal,
                doc.TaxTotal,
                doc.Total,
                doc.Notes,
                doc.CreatedBy);
        }

        Set(invoice, nameof(ExpenseInvoice.Id), doc.Id);
        Set(invoice, nameof(ExpenseInvoice.InvoiceNumber), doc.DocNumber);
        Set(invoice, nameof(ExpenseInvoice.TotalNotesApplied), doc.TotalNotesApplied);
        Set(invoice, nameof(ExpenseInvoice.Status), doc.Status);
        Set(invoice, nameof(ExpenseInvoice.JournalEntryId), doc.JournalEntryId);
        Set(invoice, nameof(ExpenseInvoice.ValidatedBy), doc.ValidatedBy);
        Set(invoice, nameof(ExpenseInvoice.ValidatedAt), doc.ValidatedAt);
        Set(invoice, nameof(ExpenseInvoice.ApprovedBy), doc.ApprovedBy);
        Set(invoice, nameof(ExpenseInvoice.ApprovedAt), doc.ApprovedAt);
        Set(invoice, nameof(ExpenseInvoice.RejectedBy), doc.RejectedBy);
        Set(invoice, nameof(ExpenseInvoice.RejectedAt), doc.RejectedAt);
        Set(invoice, nameof(ExpenseInvoice.RejectionReason), doc.RejectionReason);
        Set(invoice, nameof(ExpenseInvoice.IsActive), doc.IsActive);
        Set(invoice, nameof(ExpenseInvoice.CreatedAt), doc.CreatedAt);
        Set(invoice, nameof(ExpenseInvoice.UpdatedAt), doc.UpdatedAt);
        Set(invoice, nameof(ExpenseInvoice.CreatedBy), doc.CreatedBy);
        Set(invoice, nameof(ExpenseInvoice.UpdatedBy), doc.UpdatedBy);
        invoice.Details = doc.Details.ToList();
        return invoice;
    }

    private static void Set<T>(T target, string property, object? value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);
}
