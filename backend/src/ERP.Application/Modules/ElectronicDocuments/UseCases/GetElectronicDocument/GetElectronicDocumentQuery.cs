using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocument;

/// <summary>
/// Consulta el estado del documento electrónico por referencia al documento de origen —
/// es el dato que tiene el módulo consumidor (su propio SourceModule/SourceEntityId),
/// nunca el Id interno de ElectronicDocument.
/// </summary>
public sealed record GetElectronicDocumentQuery(string SourceModule, Guid SourceEntityId)
    : IRequest<Result<ElectronicDocumentDto?>>,
        ICompanyScopedRequest;
