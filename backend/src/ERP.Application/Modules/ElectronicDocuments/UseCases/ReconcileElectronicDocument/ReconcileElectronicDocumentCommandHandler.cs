using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;

/// <summary>
/// Caso #32 — la evidencia se persiste primero (queda aunque la consulta posterior no resuelva) y
/// después se reconsulta por el mismo <see cref="IElectronicDocumentIssuer.RetryAsync"/> del
/// Monitor/job, que nunca reenvía un comprobante con resultado desconocido. Una consulta no
/// concluyente o un origen que ya no permite procesamiento no invalidan la evidencia registrada:
/// se devuelve el detalle actualizado del documento.
/// </summary>
public sealed class ReconcileElectronicDocumentCommandHandler
    : IRequestHandler<ReconcileElectronicDocumentCommand, Result<ElectronicDocumentDetailDto>>
{
    private readonly IElectronicDocumentIssuer _issuer;
    private readonly IElectronicDocumentRepository _repository;
    private readonly IMediator _mediator;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public ReconcileElectronicDocumentCommandHandler(
        IElectronicDocumentIssuer issuer,
        IElectronicDocumentRepository repository,
        IMediator mediator,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _issuer = issuer;
        _repository = repository;
        _mediator = mediator;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<ElectronicDocumentDetailDto>> Handle(
        ReconcileElectronicDocumentCommand command,
        CancellationToken cancellationToken
    )
    {
        var document = await _repository.GetByIdAsync(
            _currentTenant.TenantId,
            command.ElectronicDocumentId,
            cancellationToken
        );
        if (
            document is null
            || (_currentCompany.HasCompanyContext && document.CompanyId != _currentCompany.CompanyId)
        )
            return Result<ElectronicDocumentDetailDto>.NotFound(
                "El documento electrónico no existe."
            );

        if (!document.RequiresSriReconciliation)
            return Result<ElectronicDocumentDetailDto>.ValidationFailure(
                $"El documento electrónico no requiere conciliación (estado actual: {document.CurrentState})."
            );

        document.RecordReconciliationEvidence(
            command.Reference,
            command.Observation,
            _currentUser.UserId
        );
        await _repository.SaveChangesAsync(cancellationToken);

        await _issuer.RetryAsync(
            _currentTenant.TenantId,
            document.Id,
            _currentUser.UserId,
            cancellationToken
        );

        return await _mediator.Send(
            new GetElectronicDocumentDetailQuery(document.Id),
            cancellationToken
        );
    }
}
