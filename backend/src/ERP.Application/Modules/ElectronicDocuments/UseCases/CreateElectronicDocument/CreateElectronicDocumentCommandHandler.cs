using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.CreateElectronicDocument;

/// <summary>
/// Delega en <see cref="IElectronicDocumentIssuer"/> — único punto que crea un ElectronicDocument,
/// para que el registro sea idéntico sin importar si lo dispara este comando (vía MediatR/API) o
/// un módulo de negocio que llama al servicio directamente en proceso.
/// </summary>
public sealed class CreateElectronicDocumentCommandHandler
    : IRequestHandler<CreateElectronicDocumentCommand, Result<ElectronicDocumentDto>>
{
    private readonly IElectronicDocumentIssuer _issuer;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CreateElectronicDocumentCommandHandler(
        IElectronicDocumentIssuer issuer,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _issuer = issuer;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public Task<Result<ElectronicDocumentDto>> Handle(
        CreateElectronicDocumentCommand command, CancellationToken cancellationToken)
    {
        var request = new RegisterElectronicDocumentRequest(
            TenantId: _currentTenant.TenantId,
            CompanyId: _currentCompany.CompanyId,
            DocumentType: command.DocumentType,
            SourceModule: command.SourceModule,
            SourceEntityId: command.SourceEntityId,
            UserId: _currentUser.UserId);

        return _issuer.RegisterAsync(request, cancellationToken);
    }
}
