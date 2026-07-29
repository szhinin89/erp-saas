using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;

/// <summary>Detalle completo de un documento electrónico para el panel del Monitor (Fase 8) — solo lectura.</summary>
public sealed record GetElectronicDocumentDetailQuery(Guid Id)
    : IRequest<Result<ElectronicDocumentDetailDto>>, ICompanyScopedRequest;
