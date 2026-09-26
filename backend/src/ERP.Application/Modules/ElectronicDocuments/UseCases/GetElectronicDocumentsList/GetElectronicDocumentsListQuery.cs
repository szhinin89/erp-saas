using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsList;

/// <summary>
/// Lista paginada de documentos electrónicos para el Monitor (Fase 8) — solo lectura,
/// siempre acotada a la empresa operativa activa (nunca cross-company).
/// ZH-TEMPORAL-CONTRACT-02: <c>DateFrom</c>/<c>DateTo</c> son días de empresa inclusivos
/// (business-date "YYYY-MM-DD"); el handler los convierte a <c>[inicioUtc, finUtc)</c> sobre
/// <c>CreatedAt</c> con <c>ICompanyClock</c>.
/// </summary>
public sealed record GetElectronicDocumentsListQuery(
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    /// <summary>Uno o más estados separados por coma (p.ej. "Signed,Sent,Received") — permite que los KPIs filtren por grupo con un solo valor.</summary>
    string? State = null,
    string? DocumentType = null,
    string? Environment = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<ElectronicDocumentsListResponse>>, ICompanyScopedRequest;
