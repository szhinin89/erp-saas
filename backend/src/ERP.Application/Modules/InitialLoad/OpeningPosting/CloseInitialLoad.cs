using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>Resultado del cierre definitivo de la Carga Inicial (<see cref="CloseInitialLoadCommand"/>).</summary>
public sealed record InitialLoadClosureDto(DateTime ClosedAt, Guid? ClosedBy, bool AlreadyClosed);

/// <summary>
/// IL-8E — cierre DEFINITIVO de la Carga Inicial de la empresa activa (<c>Company.CloseInitialLoad</c>).
/// Solo con la conciliación de apertura (<see cref="GetOpeningBalanceReconciliationQuery"/>) sin
/// ningún blocker: fecha de apertura, lotes de saldos terminados y contabilizados, Inventario/CxC/CxP
/// = Mayor, ASI vigente Posted (siempre obligatorio, aunque no haya lotes) y cuenta puente en 0. No
/// borra ni modifica historial; después del cierre <see cref="InitialLoadClosedGuard"/> rechaza todo
/// cambio a la apertura. Irreversible desde el ERP (no hay reapertura).
/// </summary>
public sealed record CloseInitialLoadCommand : IRequest<Result<InitialLoadClosureDto>>, ICompanyScopedRequest;

/// <summary>
/// Orden, en UNA transacción propia: <c>FOR UPDATE</c> de la empresa y de sus lotes de saldos (mismo
/// bloqueo que publicar/reversar el ASI y cambiar la fecha; IL-7B y la confirmación de lotes bloquean
/// su lote, la confirmación además la empresa primero) → ya cerrada devuelve el cierre existente →
/// conciliación recalculada dentro de la transacción → cualquier blocker rechaza (rollback) → cierre.
/// </summary>
public sealed partial class CloseInitialLoadCommandHandler
    : IRequestHandler<CloseInitialLoadCommand, Result<InitialLoadClosureDto>>
{
    public const string BlockedCode = "INITIAL_LOAD_CLOSE_BLOCKED";
    private const string InternalErrorMessage = "Error interno del sistema.";

    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpeningJournalEntryPostingRepository _openingJournal;
    private readonly ICompanyRepository _companies;
    private readonly IMediator _mediator;
    private readonly ILogger<CloseInitialLoadCommandHandler> _logger;

    public CloseInitialLoadCommandHandler(
        IOperationalContext ctx,
        IUnitOfWork unitOfWork,
        IOpeningJournalEntryPostingRepository openingJournal,
        ICompanyRepository companies,
        IMediator mediator,
        ILogger<CloseInitialLoadCommandHandler> logger
    )
    {
        _ctx = ctx;
        _unitOfWork = unitOfWork;
        _openingJournal = openingJournal;
        _companies = companies;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<InitialLoadClosureDto>> Handle(CloseInitialLoadCommand command, CancellationToken ct)
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _openingJournal.LockCompanyOpeningAsync(tenantId, companyId, includeBalanceBatches: true, ct);
            var company = await _companies.GetTrackedByIdForTenantAsync(companyId, tenantId, ct);
            if (company is null)
                return await RejectAsync(Result<InitialLoadClosureDto>.NotFound("Empresa no encontrada."));

            if (company.IsInitialLoadClosed)
            {
                await _unitOfWork.CommitAsync(ct);
                return Result<InitialLoadClosureDto>.Success(
                    new(company.InitialLoadClosedAt!.Value, company.InitialLoadClosedBy, AlreadyClosed: true));
            }

            var reconciliation = await _mediator.Send(new GetOpeningBalanceReconciliationQuery(), ct);
            if (!reconciliation.IsSuccess)
                return await RejectAsync(Result<InitialLoadClosureDto>.Failure(
                    reconciliation.Error ?? InternalErrorMessage, reconciliation.Code));
            var blockers = reconciliation.Value!.Blockers;
            if (blockers.Count > 0)
                return await RejectAsync(Result<InitialLoadClosureDto>.ValidationFailure(
                    "No se puede cerrar la carga inicial: " + string.Join(" · ", blockers.Select(b => b.Message)),
                    BlockedCode));

            company.CloseInitialLoad(_ctx.UserId);
            await _companies.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<InitialLoadClosureDto>.Success(
                new(company.InitialLoadClosedAt!.Value, company.InitialLoadClosedBy, AlreadyClosed: false));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogCloseFailed(ex, companyId);
            return Result<InitialLoadClosureDto>.Failure(InternalErrorMessage);
        }
    }

    private async Task<Result<InitialLoadClosureDto>> RejectAsync(Result<InitialLoadClosureDto> result)
    {
        await RollbackAsync();
        return result;
    }

    private async Task RollbackAsync()
    {
        await _unitOfWork.RollbackAsync(CancellationToken.None);
        _unitOfWork.ClearChangeTracker();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Cierre de la carga inicial de la empresa {CompanyId} falló por un error interno"
    )]
    private partial void LogCloseFailed(Exception ex, Guid companyId);
}
