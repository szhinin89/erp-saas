using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;

/// <summary>
/// Caso #32 — conciliación manual de un comprobante con resultado SRI desconocido: registra la
/// evidencia del usuario autorizado y vuelve a consultar al SRI. Nunca autoriza, rechaza ni libera
/// un reenvío por declaración del usuario.
/// </summary>
public sealed record ReconcileElectronicDocumentCommand(
    Guid ElectronicDocumentId,
    string Reference,
    string Observation
) : IRequest<Result<ElectronicDocumentDetailDto>>, ICompanyScopedRequest;
