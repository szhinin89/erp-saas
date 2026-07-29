using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDiagnosticBySource;

/// <summary>
/// Diagnóstico agnóstico de módulo — el punto de entrada que cualquier pantalla del ERP (Ventas,
/// y a futuro Retenciones/Notas/Guías cuando tengan emisión activa) usa para mostrar
/// <c>ElectronicDocumentDiagnosticPanel</c> sin conocer el Id interno de
/// <c>ElectronicDocument</c> — solo su propio <paramref name="SourceModule"/>/
/// <paramref name="SourceEntityId"/>, que ya conoce. Devuelve <c>NotFound</c> si el documento de
/// origen todavía no tiene un documento electrónico registrado (p.ej. factura aún no emitida
/// electrónicamente) — el frontend lo trata como estado vacío, no como error.
/// </summary>
public sealed record GetElectronicDocumentDiagnosticBySourceQuery(string SourceModule, Guid SourceEntityId)
    : IRequest<Result<ElectronicDocumentDiagnosticDto>>, ICompanyScopedRequest;
