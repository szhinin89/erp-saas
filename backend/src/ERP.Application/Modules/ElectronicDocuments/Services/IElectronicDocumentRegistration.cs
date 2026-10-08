using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Create-only registration for durable recovery. Never runs or resumes the pipeline.</summary>
public interface IElectronicDocumentRegistration
{
    Task<Result<ElectronicDocumentDto>> RegisterMissingAsync(
        RegisterElectronicDocumentRequest request, CancellationToken ct = default);
}
