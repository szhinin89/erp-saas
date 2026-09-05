using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetDocumentSequences;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-UI-04 — lectura mínima que faltaba para poder mostrar el estado de
/// las secuencias documentales SRI en UI: hasta esta fase, <c>ConfigureDocumentSequenceCommand</c>
/// (PUT) era el único caso de uso sobre <c>DocumentSequence</c>, sin ningún endpoint de lectura.
/// Devuelve TODAS las secuencias ya configuradas/usadas de la empresa activa — el frontend cruza
/// este listado contra los puntos de emisión (<c>emissionPointsService.list</c>) y el catálogo de
/// tipos de documento SRI (<c>catalogService.docTypes</c>) para determinar qué combinaciones
/// (EmissionPointId, DocTypeCode) todavía no tienen fila ("sin configurar"). No cambia ninguna
/// regla de dominio — solo lectura, scoping por tenant/empresa vía los query filters globales de
/// EF ya usados por <c>IDocumentSequenceRepository.GetByEmissionPointAndDocTypeAsync</c>.
/// </summary>
public sealed record GetDocumentSequencesQuery
    : IRequest<Result<IReadOnlyList<DocumentSequenceDto>>>,
        ICompanyScopedRequest;
