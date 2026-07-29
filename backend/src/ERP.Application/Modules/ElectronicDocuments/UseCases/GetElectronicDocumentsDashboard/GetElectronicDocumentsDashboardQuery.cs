using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsDashboard;

/// <summary>Indicadores del Monitor de Documentos Electrónicos — todos calculados en vivo, nunca hardcodeados.</summary>
public sealed record GetElectronicDocumentsDashboardQuery
    : IRequest<Result<ElectronicDocumentsDashboardDto>>,
        ICompanyScopedRequest;
