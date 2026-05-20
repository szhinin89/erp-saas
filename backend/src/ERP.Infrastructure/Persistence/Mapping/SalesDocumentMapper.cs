using System.Reflection;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Persistence.Mapping;

/// <summary>Traduce entre el agregado legacy <see cref="SalesBill"/> y el modelo unificado <see cref="SalesDocument"/>.</summary>
public static class SalesDocumentMapper
{
    public static SalesDocument ToDocument(SalesBill bill)
    {
        var doc = SalesDocument.CreateInvoice(
            bill.SubscriberId,
            bill.BranchId,
            bill.CustomerId,
            bill.WarehouseId,
            bill.EstabCode,
            bill.EmPointCode,
            bill.Sequential,
            bill.AccessKey,
            bill.IssueDate,
            bill.Subtotal,
            bill.VatTotal,
            bill.Total,
            bill.TotalDiscount,
            bill.PaymentMethodCode,
            bill.PaymentDays,
            bill.Notes,
            bill.CreatedBy,
            bill.CompanyId);

        // Preservar id y auditoría
        Set(doc, nameof(SalesDocument.Id), bill.Id);
        Set(doc, nameof(SalesDocument.Status), bill.Status);
        Set(doc, nameof(SalesDocument.CreatedAt), bill.CreatedAt);
        Set(doc, nameof(SalesDocument.UpdatedAt), bill.UpdatedAt);
        Set(doc, nameof(SalesDocument.CreatedBy), bill.CreatedBy);
        Set(doc, nameof(SalesDocument.UpdatedBy), bill.UpdatedBy);
        if (bill.JournalEntryId.HasValue)
            Set(doc, nameof(SalesDocument.JournalEntryId), bill.JournalEntryId);

        if (bill.XmlSignedPath is not null || bill.XmlAuthPath is not null
            || bill.AuthNumber is not null || bill.ErrorMessage is not null)
        {
            var electronic = SalesElectronicDoc.CreateShell(bill.Id, bill.SubscriberId);
            if (bill.AuthNumber is not null)
                electronic.SetAuthorization(
                    bill.AuthNumber,
                    bill.AuthDate ?? bill.IssueDate,
                    bill.XmlSignedPath,
                    bill.XmlAuthPath);
            if (!string.IsNullOrWhiteSpace(bill.ErrorMessage))
                electronic.SetError(bill.ErrorMessage);
            doc.AttachElectronic(electronic);
        }

        foreach (var line in bill.Lines)
        {
            var detail = SalesDetail.Create(
                line.SubscriberId,
                line.ProductId,
                line.ProductCode,
                line.Quantity,
                line.UnitPrice,
                line.DiscountAmount,
                line.VatCode,
                line.VatPercentage,
                line.VatTotal,
                line.Description,
                line.CreatedBy);
            typeof(SalesDetail).GetProperty(nameof(SalesDetail.Id))!.SetValue(detail, line.Id);
            detail.AssignDocumentId(bill.Id);
            doc.AddLine(detail);
        }

        return doc;
    }

    public static SalesBill ToLegacyBill(SalesDocument doc)
    {
        var electronic = doc.Electronic;
        var bill = SalesBill.Create(
            doc.SubscriberId,
            doc.BranchId ?? Guid.Empty,
            doc.CustomerId ?? Guid.Empty,
            doc.WarehouseId ?? Guid.Empty,
            doc.DocType.ToLegacySriCode(),
            doc.EstabCode ?? "",
            doc.EmPointCode ?? "",
            doc.Sequential ?? "",
            doc.AccessKey ?? electronic?.AccessKey ?? "",
            doc.IssueDate,
            doc.Subtotal,
            doc.VatTotal,
            doc.Total,
            doc.TotalDiscount,
            doc.PaymentMethodCode,
            doc.PaymentDays,
            doc.Notes,
            electronic?.XmlSignedPath,
            electronic?.XmlAuthPath,
            electronic?.AuthNumber,
            electronic?.AuthDate,
            electronic?.ErrorMessage,
            doc.CreatedBy,
            doc.CompanyId);

        typeof(SalesBill).GetProperty(nameof(SalesBill.Id))!.SetValue(bill, doc.Id);
        typeof(SalesBill).GetProperty(nameof(SalesBill.Status))!.SetValue(bill, doc.Status);
        if (doc.JournalEntryId.HasValue)
            typeof(SalesBill).GetProperty(nameof(SalesBill.JournalEntryId))!.SetValue(bill, doc.JournalEntryId);

        foreach (var line in doc.Lines)
        {
            var legacyLine = SalesBillLine.Create(
                line.SubscriberId,
                line.ProductId ?? Guid.Empty,
                line.ProductCode,
                line.Quantity,
                line.UnitPrice,
                line.DiscountAmount,
                line.VatCode,
                line.VatPercentage,
                line.VatAmount,
                line.Description,
                line.CreatedBy);
            typeof(SalesBillLine).GetProperty(nameof(SalesBillLine.Id))!.SetValue(legacyLine, line.Id);
            legacyLine.AssignBillId(doc.Id);
            bill.AddLine(legacyLine);
        }

        return bill;
    }

    public static SalesDocument ToDocument(SalesNote note, SalesBill? originalBill = null)
    {
        var doc = SalesDocument.Rehydrate();
        var docType = SalesDocumentTypeExtensions.FromNoteType(note.NoteType);

        Set(doc, nameof(SalesDocument.Id), note.Id);
        Set(doc, nameof(SalesDocument.SubscriberId), note.SubscriberId);
        Set(doc, nameof(SalesDocument.DocType), docType);
        Set(doc, nameof(SalesDocument.ReferenceDocumentId), note.OriginalBillId);
        Set(doc, nameof(SalesDocument.Reason), note.Reason);
        Set(doc, nameof(SalesDocument.EstabCode), note.EstabCode);
        Set(doc, nameof(SalesDocument.EmPointCode), note.EmPointCode);
        Set(doc, nameof(SalesDocument.Sequential), note.Sequential);
        Set(doc, nameof(SalesDocument.AccessKey), note.AccessKey);
        Set(doc, nameof(SalesDocument.IssueDate), note.IssueDate);
        Set(doc, nameof(SalesDocument.Subtotal), note.Subtotal);
        Set(doc, nameof(SalesDocument.VatTotal), note.VatTotal);
        Set(doc, nameof(SalesDocument.Total), note.Total);
        Set(doc, nameof(SalesDocument.Status), note.Status);
        Set(doc, nameof(SalesDocument.JournalEntryId), note.JournalEntryId);
        Set(doc, nameof(SalesDocument.CreatedAt), note.CreatedAt);
        Set(doc, nameof(SalesDocument.UpdatedAt), note.UpdatedAt);
        Set(doc, nameof(SalesDocument.CreatedBy), note.CreatedBy);
        Set(doc, nameof(SalesDocument.UpdatedBy), note.UpdatedBy);

        if (originalBill is not null)
        {
            Set(doc, nameof(SalesDocument.BranchId), originalBill.BranchId);
            Set(doc, nameof(SalesDocument.CustomerId), originalBill.CustomerId);
            Set(doc, nameof(SalesDocument.WarehouseId), originalBill.WarehouseId);
        }

        if (note.XmlSignedPath is not null || note.XmlAuthPath is not null || note.AuthNumber is not null)
        {
            var electronic = SalesElectronicDoc.CreateShell(note.Id, note.SubscriberId);
            electronic.SetAuthorization(
                note.AuthNumber ?? "",
                note.AuthDate ?? note.IssueDate,
                note.XmlSignedPath,
                note.XmlAuthPath);
            if (!string.IsNullOrWhiteSpace(note.ErrorMessage))
                electronic.SetError(note.ErrorMessage);
            doc.AttachElectronic(electronic);
        }

        foreach (var line in note.Lines)
            doc.AddLine(SalesDetail.FromNoteLine(line));

        return doc;
    }

    public static SalesNote ToLegacyNote(SalesDocument doc)
    {
        var noteType = doc.DocType == SalesDocumentType.CreditNote ? "CREDIT" : "DEBIT";
        var sriCode  = doc.DocType.ToLegacySriCode();

        var note = SalesNote.Create(
            doc.SubscriberId,
            doc.ReferenceDocumentId ?? Guid.Empty,
            noteType,
            doc.Reason ?? "",
            sriCode,
            doc.EstabCode ?? "",
            doc.EmPointCode ?? "",
            doc.Sequential ?? "",
            doc.AccessKey ?? "",
            doc.IssueDate,
            doc.CreatedBy);

        Set(note, nameof(SalesNote.Id), doc.Id);
        Set(note, nameof(SalesNote.Subtotal), doc.Subtotal);
        Set(note, nameof(SalesNote.VatTotal), doc.VatTotal);
        Set(note, nameof(SalesNote.Total), doc.Total);
        Set(note, nameof(SalesNote.Status), doc.Status);
        Set(note, nameof(SalesNote.JournalEntryId), doc.JournalEntryId);
        Set(note, nameof(SalesNote.CreatedAt), doc.CreatedAt);
        Set(note, nameof(SalesNote.UpdatedAt), doc.UpdatedAt);
        Set(note, nameof(SalesNote.CreatedBy), doc.CreatedBy);
        Set(note, nameof(SalesNote.UpdatedBy), doc.UpdatedBy);

        var electronic = doc.Electronic;
        if (electronic is not null)
        {
            Set(note, nameof(SalesNote.AuthNumber), electronic.AuthNumber);
            Set(note, nameof(SalesNote.AuthDate), electronic.AuthDate);
            Set(note, nameof(SalesNote.XmlSignedPath), electronic.XmlSignedPath);
            Set(note, nameof(SalesNote.XmlAuthPath), electronic.XmlAuthPath);
            Set(note, nameof(SalesNote.ErrorMessage), electronic.ErrorMessage);
        }

        if (doc.Reference is not null)
            Set(note, nameof(SalesNote.OriginalBill), ToLegacyBill(doc.Reference));

        var lines = (List<SalesNoteLine>)typeof(SalesNote)
            .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(note)!;

        foreach (var line in doc.Lines)
        {
            var legacy = (SalesNoteLine)Activator.CreateInstance(typeof(SalesNoteLine), true)!;
            Set(legacy, nameof(SalesNoteLine.Id), line.Id);
            Set(legacy, nameof(SalesNoteLine.SubscriberId), line.SubscriberId);
            Set(legacy, nameof(SalesNoteLine.SalesNoteId), doc.Id);
            Set(legacy, nameof(SalesNoteLine.ProductId), line.ProductId ?? Guid.Empty);
            Set(legacy, nameof(SalesNoteLine.ProductCode), line.ProductCode);
            Set(legacy, nameof(SalesNoteLine.Quantity), line.Quantity);
            Set(legacy, nameof(SalesNoteLine.UnitPrice), line.UnitPrice);
            Set(legacy, nameof(SalesNoteLine.VatCode), line.VatCode);
            Set(legacy, nameof(SalesNoteLine.VatPercentage), line.VatPercentage);
            Set(legacy, nameof(SalesNoteLine.Subtotal), line.Subtotal);
            Set(legacy, nameof(SalesNoteLine.VatTotal), line.VatAmount);
            Set(legacy, nameof(SalesNoteLine.Total), line.Total);
            Set(legacy, nameof(SalesNoteLine.Description), line.Description);
            lines.Add(legacy);
        }

        return note;
    }

    private static void Set<T>(T target, string property, object? value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);
}
