using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.RetryElectronicDocument;

/// <summary>
/// Reintenta manualmente un documento electrónico varado en Signed/Received, o lo reactiva
/// desde DeadLetter antes de reintentarlo. Delega en <see cref="Services.IElectronicDocumentIssuer.RetryAsync"/>.
/// </summary>
public sealed record RetryElectronicDocumentCommand(Guid ElectronicDocumentId)
    : IRequest<Result<ElectronicDocumentDetailDto>>,
        ICompanyScopedRequest;
