using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.CreateElectronicDocument;

/// <summary>
/// Registra (o reanuda, si ya existe en Draft/Failed) el documento electrónico de un documento
/// de origen ya autorizado comercialmente — corre el pipeline completo vía
/// <see cref="ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer"/>.
/// Uso principal: backfill manual de documentos que quedaron sin fila en el Monitor (p.ej.
/// autorizados antes de que existiera este mecanismo). Idempotente: si ya existe un
/// ElectronicDocument en un estado posterior a Draft/Failed para ese origen, responde Conflict.
/// </summary>
public sealed record CreateElectronicDocumentCommand(
    ElectronicDocumentType DocumentType,
    string SourceModule,
    Guid SourceEntityId
) : IRequest<Result<ElectronicDocumentDto>>, ICompanyScopedRequest;
