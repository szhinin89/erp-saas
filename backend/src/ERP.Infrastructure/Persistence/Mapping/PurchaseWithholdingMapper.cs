using System.Reflection;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Persistence.Mapping;

public static class PurchaseWithholdingMapper
{
    public static PurchaseWithholding ToWithholding(IssuedRetention retention)
    {
        var w = PurchaseWithholding.Rehydrate();
        Set(w, nameof(PurchaseWithholding.Id), retention.Id);
        Set(w, nameof(PurchaseWithholding.SubscriberId), retention.SubscriberId);
        Set(w, nameof(PurchaseWithholding.SupplierId), retention.SupplierId);
        Set(w, nameof(PurchaseWithholding.Direction), WithholdingDirection.Issued);
        Set(w, nameof(PurchaseWithholding.PurchaseDocumentId), retention.PurchBillId);
        Set(w, nameof(PurchaseWithholding.VoucherType), retention.VoucherType);
        Set(w, nameof(PurchaseWithholding.AccessKey), retention.AccessKey);
        Set(w, nameof(PurchaseWithholding.IssueDate), retention.IssueDate);
        Set(w, nameof(PurchaseWithholding.EstabCode), retention.EstablishmentCode);
        Set(w, nameof(PurchaseWithholding.EmPointCode), retention.EmissionPointCode);
        Set(w, nameof(PurchaseWithholding.Sequential), retention.Sequential);
        Set(w, nameof(PurchaseWithholding.TotalRetained), retention.TotalRetained);
        Set(w, nameof(PurchaseWithholding.Status), retention.Status);
        Set(w, nameof(PurchaseWithholding.XmlSignedPath), retention.XmlSignedPath);
        Set(w, nameof(PurchaseWithholding.XmlAuthPath), retention.XmlAuthPath);
        Set(w, nameof(PurchaseWithholding.AuthNumber), retention.AuthNumber);
        Set(w, nameof(PurchaseWithholding.AuthDate), retention.AuthDate);
        Set(w, nameof(PurchaseWithholding.ErrorMessage), retention.ErrorMessage);
        Set(w, nameof(PurchaseWithholding.JournalEntryId), retention.JournalEntryId);
        Set(w, nameof(PurchaseWithholding.CreatedAt), retention.CreatedAt);
        Set(w, nameof(PurchaseWithholding.UpdatedAt), retention.UpdatedAt);
        Set(w, nameof(PurchaseWithholding.CreatedBy), retention.CreatedBy);
        Set(w, nameof(PurchaseWithholding.UpdatedBy), retention.UpdatedBy);

        foreach (var line in retention.Lines)
        {
            var wl = PurchaseWithholdingLine.Rehydrate();
            Set(wl, nameof(PurchaseWithholdingLine.Id), line.Id);
            Set(wl, nameof(PurchaseWithholdingLine.SubscriberId), line.SubscriberId);
            Set(wl, nameof(PurchaseWithholdingLine.PurchaseWithholdingId), retention.Id);
            Set(wl, nameof(PurchaseWithholdingLine.TaxType), line.TaxType);
            Set(wl, nameof(PurchaseWithholdingLine.RetentionCode), line.RetentionCode);
            Set(wl, nameof(PurchaseWithholdingLine.TaxableBase), line.TaxableBase);
            Set(wl, nameof(PurchaseWithholdingLine.RetentionPct), line.RetentionPct);
            Set(wl, nameof(PurchaseWithholdingLine.AmountRetained), line.AmountRetained);
            Set(wl, nameof(PurchaseWithholdingLine.CreatedAt), line.CreatedAt);
            w.AddLine(wl);
        }

        return w;
    }

    public static IssuedRetention ToLegacyRetention(PurchaseWithholding w)
    {
        var retention = IssuedRetention.Create(
            w.SubscriberId,
            w.SupplierId,
            w.PurchaseDocumentId,
            w.AccessKey,
            w.IssueDate,
            w.EstabCode ?? "",
            w.EmPointCode ?? "",
            w.Sequential ?? "",
            w.CreatedBy);

        Set(retention, nameof(IssuedRetention.Id), w.Id);
        Set(retention, nameof(IssuedRetention.VoucherType), w.VoucherType);
        Set(retention, nameof(IssuedRetention.TotalRetained), w.TotalRetained);
        Set(retention, nameof(IssuedRetention.Status), w.Status);
        Set(retention, nameof(IssuedRetention.XmlSignedPath), w.XmlSignedPath);
        Set(retention, nameof(IssuedRetention.XmlAuthPath), w.XmlAuthPath);
        Set(retention, nameof(IssuedRetention.AuthNumber), w.AuthNumber);
        Set(retention, nameof(IssuedRetention.AuthDate), w.AuthDate);
        Set(retention, nameof(IssuedRetention.ErrorMessage), w.ErrorMessage);
        Set(retention, nameof(IssuedRetention.JournalEntryId), w.JournalEntryId);
        Set(retention, nameof(IssuedRetention.CreatedAt), w.CreatedAt);
        Set(retention, nameof(IssuedRetention.UpdatedAt), w.UpdatedAt);
        Set(retention, nameof(IssuedRetention.UpdatedBy), w.UpdatedBy);

        var lines = (List<PurchRetentionLine>)typeof(IssuedRetention)
            .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(retention)!;

        foreach (var line in w.Lines)
        {
            var legacy = PurchRetentionLine.Create(
                line.SubscriberId,
                line.TaxType,
                line.RetentionCode,
                line.TaxableBase,
                line.RetentionPct,
                line.AmountRetained,
                null,
                w.CreatedBy);
            Set(legacy, nameof(PurchRetentionLine.Id), line.Id);
            legacy.AssignRetentionId(w.Id);
            lines.Add(legacy);
        }

        return retention;
    }

    private static void Set<T>(T target, string property, object? value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);
}
