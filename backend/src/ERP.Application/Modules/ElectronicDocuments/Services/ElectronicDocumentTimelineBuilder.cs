using ERP.Application.Audit;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Reconstruye el timeline de un documento electrónico a partir de <see cref="ElectronicDocumentAudit"/>
/// (append-only, real) — nunca inventa eventos. Compartido por el detalle del Monitor y la consulta
/// de timeline dedicada para no duplicar la lógica de mapeo/duración.
/// </summary>
public static class ElectronicDocumentTimelineBuilder
{
    public static async Task<IReadOnlyList<ElectronicDocumentAudit>> FetchRecordsAsync(
        IAuditReader<ElectronicDocumentAudit> auditReader,
        Guid tenantId,
        Guid documentId,
        int take,
        CancellationToken ct)
        => await auditReader.GetByEntityAsync(tenantId, documentId, take, ct);

    /// <summary>Construye el timeline a partir de registros ya obtenidos (más reciente primero) — evita una segunda consulta.</summary>
    public static IReadOnlyList<ElectronicDocumentTimelineEventDto> Build(
        IReadOnlyList<ElectronicDocumentAudit> records)
    {
        var ordered = records.OrderBy(r => r.OccurredAtUtc).ToList();

        var events = new List<ElectronicDocumentTimelineEventDto>(ordered.Count);
        DateTime? previous = null;

        foreach (var record in ordered)
        {
            double? durationMinutes = previous.HasValue
                ? (record.OccurredAtUtc - previous.Value).TotalMinutes
                : null;

            events.Add(new ElectronicDocumentTimelineEventDto(
                record.Action,
                record.FromState?.ToString(),
                record.ToState.ToString(),
                record.OccurredAtUtc,
                record.UserName,
                durationMinutes));

            previous = record.OccurredAtUtc;
        }

        return events;
    }
}
