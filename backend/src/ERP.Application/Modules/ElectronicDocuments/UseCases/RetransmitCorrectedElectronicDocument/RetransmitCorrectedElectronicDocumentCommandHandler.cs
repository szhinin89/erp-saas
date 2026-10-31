using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicDocuments.UseCases.RetransmitCorrectedElectronicDocument;

/// <summary>
/// R12 Fase B — delega la operación completa en <see cref="IElectronicDocumentCorrectedRetransmission"/>
/// (elegibilidad, corrección demostrada, reclamo bajo lock, una transmisión) y devuelve el detalle
/// actualizado del Monitor. No toca la operación comercial: inventario, caja, CxC/CxP y contabilidad
/// siguen siendo los de la venta/retención ya confirmada.
/// </summary>
public sealed class RetransmitCorrectedElectronicDocumentCommandHandler
    : IRequestHandler<RetransmitCorrectedElectronicDocumentCommand, Result<ElectronicDocumentDetailDto>>
{
    private readonly IElectronicDocumentCorrectedRetransmission _retransmission;
    private readonly IElectronicDocumentRepository _repository;
    private readonly IMediator _mediator;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public RetransmitCorrectedElectronicDocumentCommandHandler(
        IElectronicDocumentCorrectedRetransmission retransmission,
        IElectronicDocumentRepository repository,
        IMediator mediator,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _retransmission = retransmission;
        _repository = repository;
        _mediator = mediator;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<ElectronicDocumentDetailDto>> Handle(
        RetransmitCorrectedElectronicDocumentCommand command,
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

        var result = await _retransmission.RetransmitCorrectedAsync(
            new CorrectedRetransmissionRequest(
                _currentTenant.TenantId,
                document.Id,
                command.Reason,
                command.IdempotencyKey,
                _currentUser.UserId
            ),
            cancellationToken
        );
        if (!result.IsSuccess)
            return Result<ElectronicDocumentDetailDto>.Failure(result.Error!, result.Code);

        return await _mediator.Send(
            new GetElectronicDocumentDetailQuery(document.Id),
            cancellationToken
        );
    }
}
