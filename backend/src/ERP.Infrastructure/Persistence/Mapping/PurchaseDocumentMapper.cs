using System.Reflection;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Infrastructure.Persistence.Converters;

namespace ERP.Infrastructure.Persistence.Mapping;

public static class PurchaseDocumentMapper
{
    public static PurchaseDocument ToDocument(PurchBill bill)
    {
        var doc = PurchaseDocument.Rehydrate();
        Set(doc, nameof(PurchaseDocument.Id), bill.Id);
        Set(doc, nameof(PurchaseDocument.SubscriberId), bill.SubscriberId);
        Set(doc, nameof(PurchaseDocument.BusinessPartnerId), bill.BusinessPartnerId);
        Set(doc, nameof(PurchaseDocument.DocType), PurchaseDocumentType.Invoice);
        Set(doc, nameof(PurchaseDocument.DocNumber), bill.InvoiceNumber);
        Set(doc, nameof(PurchaseDocument.AccessKey), bill.AccessKey);
        Set(doc, nameof(PurchaseDocument.IssueDate), bill.InvoiceDate);
        Set(doc, nameof(PurchaseDocument.DueDate), bill.DueDate);
        Set(doc, nameof(PurchaseDocument.Subtotal), bill.Subtotal);
        Set(doc, nameof(PurchaseDocument.VatTotal), bill.VatTotal);
        Set(doc, nameof(PurchaseDocument.Total), bill.Total);
        Set(doc, nameof(PurchaseDocument.TotalNotesApplied), bill.TotalNotesApplied);
        Set(doc, nameof(PurchaseDocument.Status), bill.Status.ToString());
        Set(doc, nameof(PurchaseDocument.PaymentTerms), bill.PaymentTerms);
        Set(doc, nameof(PurchaseDocument.Notes), bill.Notes);
        Set(doc, nameof(PurchaseDocument.XmlPath), bill.XmlPath);
        Set(doc, nameof(PurchaseDocument.JournalEntryId), bill.JournalEntryId);
        Set(doc, nameof(PurchaseDocument.ValidatedBy), bill.ValidatedBy);
        Set(doc, nameof(PurchaseDocument.ValidatedAt), bill.ValidatedAt);
        Set(doc, nameof(PurchaseDocument.ApprovedBy), bill.ApprovedBy);
        Set(doc, nameof(PurchaseDocument.ApprovedAt), bill.ApprovedAt);
        Set(doc, nameof(PurchaseDocument.RejectedBy), bill.RejectedBy);
        Set(doc, nameof(PurchaseDocument.RejectedAt), bill.RejectedAt);
        Set(doc, nameof(PurchaseDocument.RejectionReason), bill.RejectionReason);
        Set(doc, nameof(PurchaseDocument.CreatedAt), bill.CreatedAt);
        Set(doc, nameof(PurchaseDocument.UpdatedAt), bill.UpdatedAt);
        Set(doc, nameof(PurchaseDocument.CreatedBy), bill.CreatedBy);
        Set(doc, nameof(PurchaseDocument.UpdatedBy), bill.UpdatedBy);

        foreach (var line in bill.Lines)
            doc.AddLine(PurchaseDetail.FromBillLine(line));

        return doc;
    }

    public static PurchBill ToLegacyBill(PurchaseDocument doc)
    {
        var bill = PurchBill.Create(
            doc.SubscriberId,
            doc.BusinessPartnerId ?? Guid.Empty,
            doc.DocNumber,
            doc.AccessKey,
            doc.XmlPath,
            doc.IssueDate,
            doc.DueDate,
            doc.PaymentTerms ?? "",
            doc.Notes,
            doc.CreatedBy);

        Set(bill, nameof(PurchBill.Id), doc.Id);
        Set(bill, nameof(PurchBill.Subtotal), doc.Subtotal);
        Set(bill, nameof(PurchBill.VatTotal), doc.VatTotal);
        Set(bill, nameof(PurchBill.Total), doc.Total);
        Set(bill, nameof(PurchBill.TotalNotesApplied), doc.TotalNotesApplied);
        Set(bill, nameof(PurchBill.Status), Enum.Parse<PurchaseStatus>(doc.Status));
        Set(bill, nameof(PurchBill.JournalEntryId), doc.JournalEntryId);
        Set(bill, nameof(PurchBill.ValidatedBy), doc.ValidatedBy);
        Set(bill, nameof(PurchBill.ValidatedAt), doc.ValidatedAt);
        Set(bill, nameof(PurchBill.ApprovedBy), doc.ApprovedBy);
        Set(bill, nameof(PurchBill.ApprovedAt), doc.ApprovedAt);
        Set(bill, nameof(PurchBill.RejectedBy), doc.RejectedBy);
        Set(bill, nameof(PurchBill.RejectedAt), doc.RejectedAt);
        Set(bill, nameof(PurchBill.RejectionReason), doc.RejectionReason);
        Set(bill, nameof(PurchBill.CreatedAt), doc.CreatedAt);
        Set(bill, nameof(PurchBill.UpdatedAt), doc.UpdatedAt);
        Set(bill, nameof(PurchBill.CreatedBy), doc.CreatedBy);
        Set(bill, nameof(PurchBill.UpdatedBy), doc.UpdatedBy);

        var lines = (List<PurchBillLine>)typeof(PurchBill)
            .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(bill)!;

        foreach (var line in doc.Lines)
        {
            var legacy = (PurchBillLine)Activator.CreateInstance(typeof(PurchBillLine), true)!;
            Set(legacy, nameof(PurchBillLine.Id), line.Id);
            Set(legacy, nameof(PurchBillLine.SubscriberId), line.SubscriberId);
            Set(legacy, nameof(PurchBillLine.PurchBillId), doc.Id);
            Set(legacy, nameof(PurchBillLine.ProductId), line.ProductId);
            Set(legacy, nameof(PurchBillLine.Description), line.Description);
            Set(legacy, nameof(PurchBillLine.Quantity), line.Quantity);
            Set(legacy, nameof(PurchBillLine.UnitPrice), line.UnitPrice);
            Set(legacy, nameof(PurchBillLine.DiscountPct), line.DiscountPct);
            Set(legacy, nameof(PurchBillLine.Subtotal), line.Subtotal);
            Set(legacy, nameof(PurchBillLine.VatPct), line.VatPercentage);
            Set(legacy, nameof(PurchBillLine.VatAmount), line.VatAmount);
            Set(legacy, nameof(PurchBillLine.Total), line.Total);
            Set(legacy, nameof(PurchBillLine.CreatedAt), line.CreatedAt);
            Set(legacy, nameof(PurchBillLine.UpdatedAt), line.UpdatedAt);
            Set(legacy, nameof(PurchBillLine.CreatedBy), line.CreatedBy);
            Set(legacy, nameof(PurchBillLine.UpdatedBy), line.UpdatedBy);
            lines.Add(legacy);
        }

        return bill;
    }

    public static PurchaseDocument ToDocument(PurchNote note)
    {
        var doc = PurchaseDocument.Rehydrate();
        var refId = note.PurchBillId ?? note.ExpenseInvoiceId;

        Set(doc, nameof(PurchaseDocument.Id), note.Id);
        Set(doc, nameof(PurchaseDocument.SubscriberId), note.SubscriberId);
        Set(doc, nameof(PurchaseDocument.BusinessPartnerId), note.BusinessPartnerId);
        Set(doc, nameof(PurchaseDocument.DocType), PurchaseDocumentTypeConversions.FromNoteType(note.NoteType));
        Set(doc, nameof(PurchaseDocument.DocNumber), $"{note.EstabCode}-{note.EmPointCode}-{note.Sequential}");
        Set(doc, nameof(PurchaseDocument.AccessKey), note.AccessKey);
        Set(doc, nameof(PurchaseDocument.IssueDate), note.IssueDate);
        Set(doc, nameof(PurchaseDocument.Subtotal), note.Subtotal);
        Set(doc, nameof(PurchaseDocument.VatTotal), note.VatTotal);
        Set(doc, nameof(PurchaseDocument.Total), note.Total);
        Set(doc, nameof(PurchaseDocument.Status), note.Status);
        Set(doc, nameof(PurchaseDocument.Reason), note.Reason);
        Set(doc, nameof(PurchaseDocument.ReferenceDocumentId), refId);
        Set(doc, nameof(PurchaseDocument.XmlPath), note.XmlPath);
        Set(doc, nameof(PurchaseDocument.JournalEntryId), note.JournalEntryId);
        Set(doc, nameof(PurchaseDocument.CreatedAt), note.CreatedAt);
        Set(doc, nameof(PurchaseDocument.UpdatedAt), note.UpdatedAt);
        Set(doc, nameof(PurchaseDocument.CreatedBy), note.CreatedBy);
        Set(doc, nameof(PurchaseDocument.UpdatedBy), note.UpdatedBy);

        foreach (var line in note.Lines)
            doc.AddLine(PurchaseDetail.FromNoteLine(line));

        return doc;
    }

    public static PurchNote ToLegacyNote(
        PurchaseDocument doc,
        Guid? purchBillId = null,
        Guid? expenseInvoiceId = null)
    {
        var note = PurchNote.Create(
            doc.SubscriberId,
            doc.BusinessPartnerId ?? Guid.Empty,
            purchBillId,
            expenseInvoiceId,
            PurchaseDocumentTypeConversions.ToNoteType(doc.DocType),
            doc.Reason ?? "",
            doc.AccessKey ?? "",
            doc.IssueDate,
            ParseEstab(doc.DocNumber),
            ParseEmPoint(doc.DocNumber),
            ParseSequential(doc.DocNumber),
            doc.CreatedBy);

        Set(note, nameof(PurchNote.Id), doc.Id);
        Set(note, nameof(PurchNote.Subtotal), doc.Subtotal);
        Set(note, nameof(PurchNote.VatTotal), doc.VatTotal);
        Set(note, nameof(PurchNote.Total), doc.Total);
        Set(note, nameof(PurchNote.Status), doc.Status);
        Set(note, nameof(PurchNote.XmlPath), doc.XmlPath);
        Set(note, nameof(PurchNote.JournalEntryId), doc.JournalEntryId);
        Set(note, nameof(PurchNote.CreatedAt), doc.CreatedAt);
        Set(note, nameof(PurchNote.UpdatedAt), doc.UpdatedAt);
        Set(note, nameof(PurchNote.CreatedBy), doc.CreatedBy);
        Set(note, nameof(PurchNote.UpdatedBy), doc.UpdatedBy);

        if (doc.Reference is not null && doc.Reference.DocType == PurchaseDocumentType.Invoice)
            Set(note, nameof(PurchNote.PurchBill), ToLegacyBill(doc.Reference));

        var lines = (List<PurchNoteLine>)typeof(PurchNote)
            .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(note)!;

        foreach (var line in doc.Lines)
        {
            var legacy = (PurchNoteLine)Activator.CreateInstance(typeof(PurchNoteLine), true)!;
            Set(legacy, nameof(PurchNoteLine.Id), line.Id);
            Set(legacy, nameof(PurchNoteLine.SubscriberId), line.SubscriberId);
            Set(legacy, nameof(PurchNoteLine.PurchNoteId), doc.Id);
            Set(legacy, nameof(PurchNoteLine.ProductId), line.ProductId);
            Set(legacy, nameof(PurchNoteLine.Description), line.Description);
            Set(legacy, nameof(PurchNoteLine.Quantity), line.Quantity);
            Set(legacy, nameof(PurchNoteLine.UnitPrice), line.UnitPrice);
            Set(legacy, nameof(PurchNoteLine.Subtotal), line.Subtotal);
            Set(legacy, nameof(PurchNoteLine.VatAmount), line.VatAmount);
            Set(legacy, nameof(PurchNoteLine.Total), line.Total);
            lines.Add(legacy);
        }

        return note;
    }

    private static string ParseEstab(string? docNumber)
    {
        var parts = (docNumber ?? "").Split('-');
        return parts.Length > 0 ? parts[0] : "";
    }

    private static string ParseEmPoint(string? docNumber)
    {
        var parts = (docNumber ?? "").Split('-');
        return parts.Length > 1 ? parts[1] : "";
    }

    private static string ParseSequential(string? docNumber)
    {
        var parts = (docNumber ?? "").Split('-');
        return parts.Length > 2 ? parts[2] : "";
    }

    private static void Set<T>(T target, string property, object? value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);
}
