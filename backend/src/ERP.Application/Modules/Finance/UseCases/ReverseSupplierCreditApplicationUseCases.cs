using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Finance.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 7 — reversa una aplicación previa de <c>SupplierCredit</c> contra una
/// <c>PurchasePayable</c> destino: mismo orden de locks que <c>ApplySupplierCreditUseCases</c>
/// (Lock A destino → Lock B, §15.4), idempotente (§16.2, fila <c>ReverseApplication</c>).
/// <c>TargetPurchasePayableId</c> se recibe explícito (redundante con el ya persistido en el
/// movimiento original) porque Lock A debe adquirirse ANTES de poder cargar el agregado que lo
/// confirma — el propio handler revalida bajo lock que coincide con el movimiento real (§9.3).
/// </summary>
public sealed record ReverseSupplierCreditApplicationCommand(
    Guid SupplierCreditId,
    Guid OriginalMovementId,
    Guid TargetPurchasePayableId,
    Guid ClientRequestId
) : IRequest<Result<SupplierCreditDto>>, ICompanyScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class ReverseSupplierCreditApplicationValidator
    : AbstractValidator<ReverseSupplierCreditApplicationCommand>
{
    public ReverseSupplierCreditApplicationValidator()
    {
        RuleFor(x => x.SupplierCreditId).NotEmpty();
        RuleFor(x => x.OriginalMovementId).NotEmpty();
        RuleFor(x => x.TargetPurchasePayableId).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class ReverseSupplierCreditApplicationHandler
    : IRequestHandler<ReverseSupplierCreditApplicationCommand, Result<SupplierCreditDto>>
{
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IPurchasePayableRepository _payableRepo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ReverseSupplierCreditApplicationHandler(
        ISupplierCreditRepository creditRepo,
        IPurchasePayableRepository payableRepo,
        IPurchaseReturnRepository purchaseReturnRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditRepo = creditRepo;
        _payableRepo = payableRepo;
        _purchaseReturnRepo = purchaseReturnRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<SupplierCreditDto>> Handle(
        ReverseSupplierCreditApplicationCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceId = await _payableRepo.GetPurchaseInvoiceIdAsync(
                tid,
                cmd.TargetPurchasePayableId,
                ct
            );
            if (purchaseInvoiceId is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.NotFound(
                    "La cuenta por pagar destino no existe."
                );
            }

            await _purchaseReturnRepo.AcquireFinancialLockAsync(tid, purchaseInvoiceId.Value, ct);
            await _creditRepo.AcquireLockAsync(tid, cmd.SupplierCreditId, ct);

            var payable = await _payableRepo.GetByIdAsync(tid, cmd.TargetPurchasePayableId, ct);
            if (payable is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.NotFound(
                    "La cuenta por pagar destino no existe."
                );
            }

            var credit = await _creditRepo.GetByIdAsync(tid, cmd.SupplierCreditId, ct);
            if (credit is null)
            {
                await _uow.RollbackAsync(ct);
                // SC-001
                return Result<SupplierCreditDto>.NotFound(
                    "El crédito de proveedor indicado no existe."
                );
            }

            // ── Idempotencia (§16.2) ──
            var existingReversal = credit.Movements.FirstOrDefault(m =>
                m.ClientRequestId == cmd.ClientRequestId
                && m.MovementType == SupplierCreditMovementType.ReversalOfApplication
            );
            if (existingReversal is not null)
            {
                await _uow.RollbackAsync(ct);
                var expectedHash = ComputeReversePayloadHash(cmd.OriginalMovementId);
                return existingReversal.RequestPayloadHash == expectedHash
                    ? Result<SupplierCreditDto>.Success(Map.ToDto(credit))
                    // SC-006
                    : Result<SupplierCreditDto>.ValidationFailure(
                        "Ya existe una solicitud de reversa con este identificador pero con datos distintos."
                    );
            }

            var original = credit.Movements.FirstOrDefault(m =>
                m.Id == cmd.OriginalMovementId
                && m.MovementType == SupplierCreditMovementType.Application
            );
            if (original is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.ValidationFailure(
                    "El movimiento original no existe o no es una aplicación de crédito."
                );
            }
            if (original.TargetPurchasePayableId != cmd.TargetPurchasePayableId)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.ValidationFailure(
                    "La cuenta por pagar indicada no corresponde al destino real del movimiento original."
                );
            }

            // ── SC-014 (§5.1 caso 5) — revalidado bajo lock: la CxP destino no puede estar
            // cancelled al momento de revertir; si lo está, es un callejón sin salida documentado
            // (nunca hay reversa posible mientras el destino permanezca cancelled). Este guard no
            // existe dentro de PurchasePayable.ReverseSupplierCredit (solo valida montos) —
            // responsabilidad explícita de Application, según diseño. ──
            if (payable.Status == "cancelled")
            {
                await _uow.RollbackAsync(ct);
                // SC-014
                return Result<SupplierCreditDto>.ValidationFailure(
                    "No se puede revertir esta aplicación de crédito porque la cuenta por pagar destino ya fue anulada."
                );
            }

            var hash = ComputeReversePayloadHash(cmd.OriginalMovementId);

            try
            {
                credit.ReverseApplication(cmd.OriginalMovementId, uid, cmd.ClientRequestId, hash);
                payable.ReverseSupplierCredit(original.Amount, uid);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // SC-011 (movimiento ya revertido) u otro guard de dominio.
                return Result<SupplierCreditDto>.ValidationFailure(ex.Message);
            }

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await _uow.RollbackAsync(ct);
                // SC-010
                return Result<SupplierCreditDto>.ValidationFailure(
                    "El crédito de proveedor fue modificado concurrentemente. Intente nuevamente."
                );
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                // §16.2bis — mismo criterio conservador que ApplySupplierCreditHandler: colisión
                // cruzada de ClientRequestId contra otro agregado, fuera del alcance de esta fase
                // extender el repositorio para localizarlo — se rechaza sin intentar el snapshot
                // cacheado (correcto: nunca es un reintento legítimo de esta misma operación).
                await _uow.RollbackAsync(ct);
                // SC-006
                return Result<SupplierCreditDto>.ValidationFailure(
                    "Ya existe una solicitud con este identificador de idempotencia."
                );
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierCreditDto>.Success(Map.ToDto(credit));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SupplierCreditDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Huella determinista (§16.2, diseño línea 1014): OperationType+ReversalOfMovementId.</summary>
    public static string ComputeReversePayloadHash(Guid originalMovementId)
    {
        var canonical = string.Join(
            "",
            "ReverseSupplierCreditApplication",
            originalMovementId.ToString("D")
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
