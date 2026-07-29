using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentTimeline;

/// <summary>Timeline de un documento electrónico (reconstruido desde su auditoría append-only).</summary>
public sealed record GetElectronicDocumentTimelineQuery(Guid Id)
    : IRequest<Result<IReadOnlyList<ElectronicDocumentTimelineEventDto>>>, ICompanyScopedRequest;
