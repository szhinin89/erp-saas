using System.Reflection;
using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Persistence.Mapping;

public static class SalesWithholdingMapper
{
    public static SalesWithholding ToWithholding(SalesRetention retention)
    {
        var w = SalesWithholding.Rehydrate();
        Set(w, nameof(SalesWithholding.Id), retention.Id);
        Set(w, nameof(SalesWithholding.SubscriberId), retention.SubscriberId);
        Set(w, nameof(SalesWithholding.CustomerId), retention.CustomerId);
        Set(w, nameof(SalesWithholding.Direction), WithholdingDirection.Received);
        Set(w, nameof(SalesWithholding.SalesDocumentId), retention.SalesBillId);
        Set(w, nameof(SalesWithholding.VoucherType), retention.VoucherType);
        Set(w, nameof(SalesWithholding.AccessKey), retention.AccessKey);
        Set(w, nameof(SalesWithholding.IssueDate), retention.IssueDate);
        Set(w, nameof(SalesWithholding.TotalRetained), retention.TotalRetained);
        Set(w, nameof(SalesWithholding.Status), retention.Status);
        Set(w, nameof(SalesWithholding.XmlPath), retention.XmlPath);
        Set(w, nameof(SalesWithholding.JournalEntryId), retention.JournalEntryId);
        Set(w, nameof(SalesWithholding.CreatedAt), retention.CreatedAt);
        Set(w, nameof(SalesWithholding.UpdatedAt), retention.UpdatedAt);
        Set(w, nameof(SalesWithholding.CreatedBy), retention.CreatedBy);
        Set(w, nameof(SalesWithholding.UpdatedBy), retention.UpdatedBy);

        foreach (var line in retention.Lines)
        {
            var wl = SalesWithholdingLine.Rehydrate();
            Set(wl, nameof(SalesWithholdingLine.Id), line.Id);
            Set(wl, nameof(SalesWithholdingLine.SubscriberId), line.SubscriberId);
            Set(wl, nameof(SalesWithholdingLine.SalesWithholdingId), retention.Id);
            Set(wl, nameof(SalesWithholdingLine.TaxType), line.TaxType);
            Set(wl, nameof(SalesWithholdingLine.RetentionCode), line.RetentionCode);
            Set(wl, nameof(SalesWithholdingLine.TaxableBase), line.TaxableBase);
            Set(wl, nameof(SalesWithholdingLine.RetentionPct), line.RetentionPct);
            Set(wl, nameof(SalesWithholdingLine.AmountRetained), line.AmountRetained);
            Set(wl, nameof(SalesWithholdingLine.CreatedAt), line.CreatedAt);
            w.AddLine(wl);
        }

        return w;
    }

    public static SalesRetention ToLegacyRetention(SalesWithholding w)
    {
        var retention = SalesRetention.Create(
            w.SubscriberId,
            w.CustomerId ?? Guid.Empty,
            w.AccessKey,
            w.IssueDate,
            w.TotalRetained,
            w.SalesDocumentId,
            w.XmlPath,
            w.CreatedBy);

        Set(retention, nameof(SalesRetention.Id), w.Id);
        Set(retention, nameof(SalesRetention.Status), w.Status);
        Set(retention, nameof(SalesRetention.JournalEntryId), w.JournalEntryId);
        Set(retention, nameof(SalesRetention.CreatedAt), w.CreatedAt);
        Set(retention, nameof(SalesRetention.UpdatedAt), w.UpdatedAt);
        Set(retention, nameof(SalesRetention.UpdatedBy), w.UpdatedBy);

        var lines = (List<SalesRetentionLine>)typeof(SalesRetention)
            .GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(retention)!;

        foreach (var line in w.Lines)
        {
            var legacy = SalesRetentionLine.Create(
                line.SubscriberId,
                line.TaxType,
                line.RetentionCode,
                line.TaxableBase,
                line.RetentionPct,
                line.AmountRetained,
                w.CreatedBy);
            Set(legacy, nameof(SalesRetentionLine.Id), line.Id);
            legacy.AssignRetentionId(w.Id);
            lines.Add(legacy);
        }

        return retention;
    }

    private static void Set<T>(T target, string property, object? value) =>
        typeof(T).GetProperty(property)!.SetValue(target, value);
}
