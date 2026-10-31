using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.RetransmitCorrectedElectronicDocument;

/// <summary>
/// R12 Fase B — acción explícita del Monitor: corregir y retransmitir con la misma clave un comprobante
/// rechazado. <see cref="IdempotencyKey"/> la genera el cliente por solicitud confirmada: repetirla
/// nunca produce otro envío.
/// </summary>
public sealed record RetransmitCorrectedElectronicDocumentCommand(
    Guid ElectronicDocumentId,
    string Reason,
    Guid IdempotencyKey
) : IRequest<Result<ElectronicDocumentDetailDto>>, ICompanyScopedRequest;
