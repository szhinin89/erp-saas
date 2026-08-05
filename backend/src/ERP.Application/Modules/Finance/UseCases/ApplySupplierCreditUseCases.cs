using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record SupplierCreditMovementDto(
    Guid Id,
    string MovementType,
    decimal Amount,
    Guid? TargetPurchasePayableId,
    Guid? ReversalOfMovementId,
    DateTime CreatedAtUtc
);

public sealed record SupplierCreditDto(
    Guid Id,
    Guid SupplierId,
    Guid BranchId,
    string CurrencyCode,
    Guid SourcePurchaseReturnId,
    decimal OriginalAmount,
    decimal AvailableAmount,
    bool IsOpen,
    IReadOnlyList<SupplierCreditMovementDto> Movements
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 7 — aplica un <c>SupplierCredit</c> existente contra una <c>PurchasePayable</c>
/// destino: Lock A (destino) → Lock B, en ese orden fijo (§15.4), idempotente (§16.2, fila
/// <c>ApplyToPayable</c>).
/// </summary>
public sealed record ApplySupplierCreditCommand(
    Guid SupplierCreditId,
    Guid TargetPurchasePayableId,
    decimal Amount,
    Guid ClientRequestId
) : IRequest<Result<SupplierCreditDto>>, ICompanyScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class ApplySupplierCreditValidator : AbstractValidator<ApplySupplierCreditCommand>
{
    public ApplySupplierCreditValidator()
    {
        RuleFor(x => x.SupplierCreditId).NotEmpty();
        RuleFor(x => x.TargetPurchasePayableId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class ApplySupplierCreditHandler
    : IRequestHandler<ApplySupplierCreditCommand, Result<SupplierCreditDto>>
{
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly IPurchasePayableRepository _payableRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ApplySupplierCreditHandler(
        ISupplierCreditRepository creditRepo,
        IPurchasePayableRepository payableRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReturnRepository purchaseReturnRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditRepo = creditRepo;
        _payableRepo = payableRepo;
        _invoiceRepo = invoiceRepo;
        _purchaseReturnRepo = purchaseReturnRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<SupplierCreditDto>> Handle(
        ApplySupplierCreditCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // §15.4: Lock A (del PurchasePayable destino) siempre antes de Lock B (del
        // SupplierCredit). Descubrimiento sin tracking del PurchaseInvoiceId dueño del destino
        // (mismo patrón GetPurchaseInvoiceIdAsync ya usado por RegisterPaymentCommandHandler/
        // IssueWithholdingUseCases/AuthorizePurchaseReturnUseCases) — garantiza que la recarga
        // posterior (después del lock) sea la primera lectura tracking, genuinamente fresca.
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
                // SC-002 — el destino no existe.
                return Result<SupplierCreditDto>.ValidationFailure(
                    "La cuenta por pagar destino no existe."
                );
            }

            await _purchaseReturnRepo.AcquireFinancialLockAsync(tid, purchaseInvoiceId.Value, ct);
            await _creditRepo.AcquireLockAsync(tid, cmd.SupplierCreditId, ct);

            var payable = await _payableRepo.GetByIdAsync(tid, cmd.TargetPurchasePayableId, ct);
            if (payable is null)
            {
                await _uow.RollbackAsync(ct);
                // SC-002
                return Result<SupplierCreditDto>.ValidationFailure(
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

            // ── Idempotencia (§16.2) — el índice único real es
            // (TenantId, ClientRequestId) sobre SupplierCreditMovement, acotado en memoria a los
            // movimientos YA cargados de este agregado (Include(Movements) en GetByIdAsync). ──
            var existingApplication = credit.Movements.FirstOrDefault(m =>
                m.ClientRequestId == cmd.ClientRequestId
                && m.MovementType == SupplierCreditMovementType.Application
            );
            if (existingApplication is not null)
            {
                await _uow.RollbackAsync(ct);
                var expectedHash = ComputeApplyPayloadHash(
                    cmd.SupplierCreditId,
                    cmd.TargetPurchasePayableId,
                    cmd.Amount
                );
                return existingApplication.RequestPayloadHash == expectedHash
                    ? Result<SupplierCreditDto>.Success(Map.ToDto(credit))
                    // SC-006
                    : Result<SupplierCreditDto>.ValidationFailure(
                        "Ya existe una solicitud de aplicación con este identificador pero con datos distintos."
                    );
            }

            // ── Revalidación bajo lock (§9.3, §12.2) ──
            if (payable.Status == "cancelled")
            {
                await _uow.RollbackAsync(ct);
                // SC-002
                return Result<SupplierCreditDto>.ValidationFailure(
                    "No se puede aplicar un crédito de proveedor sobre una cuenta por pagar anulada."
                );
            }
            if (credit.SupplierId != payable.SupplierId)
            {
                await _uow.RollbackAsync(ct);
                // SC-004
                return Result<SupplierCreditDto>.ValidationFailure(
                    "El crédito de proveedor pertenece a un proveedor distinto al de la cuenta por pagar destino."
                );
            }

            var invoice = await _invoiceRepo.GetByIdAsync(tid, payable.PurchaseId, ct);
            if (invoice is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.NotFound(
                    "La compra asociada a la cuenta por pagar destino no fue encontrada."
                );
            }
            if (
                !string.Equals(
                    invoice.CurrencyCode,
                    credit.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                await _uow.RollbackAsync(ct);
                // SC-005
                return Result<SupplierCreditDto>.ValidationFailure(
                    "La moneda del crédito de proveedor no coincide con la de la cuenta por pagar destino."
                );
            }
            if (cmd.Amount > credit.AvailableAmount)
            {
                await _uow.RollbackAsync(ct);
                // SC-003
                return Result<SupplierCreditDto>.ValidationFailure(
                    "El monto a aplicar excede el saldo disponible del crédito de proveedor."
                );
            }

            var hash = ComputeApplyPayloadHash(
                cmd.SupplierCreditId,
                cmd.TargetPurchasePayableId,
                cmd.Amount
            );

            try
            {
                credit.ApplyToPayable(
                    cmd.TargetPurchasePayableId,
                    cmd.Amount,
                    uid,
                    cmd.ClientRequestId,
                    hash
                );
                payable.ApplySupplierCredit(cmd.Amount, uid);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                // SC-003 (sobreaplicación defensiva) u otro guard de dominio.
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
                // §16.2bis — colisión de ClientRequestId por una causa distinta al lock (mismo
                // ClientRequestId reutilizado contra otro SupplierCreditId/destino, fuera del
                // agregado que esta operación ya tenía cargado). Ventana estructuralmente pequeña
                // (Lock B ya serializa esta operación sobre el mismo SupplierCredit). Sin un
                // método de repositorio adicional para localizar el movimiento ya persistido de
                // OTRO agregado (fuera del alcance autorizado de esta fase — "Archivos existentes
                // a modificar: Ninguno"), se rechaza de forma conservadora sin intentar retornar
                // el snapshot cacheado — comportamiento correcto de todas formas, porque una
                // colisión cruzada de agregados nunca es un reintento legítimo de esta misma
                // operación.
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

    /// <summary>Huella determinista (§16.2, diseño línea 1013): OperationType+SupplierCreditId+TargetPurchasePayableId+Amount.</summary>
    public static string ComputeApplyPayloadHash(
        Guid supplierCreditId,
        Guid targetPurchasePayableId,
        decimal amount
    )
    {
        var canonical = string.Join(
            "",
            "ApplySupplierCredit",
            supplierCreditId.ToString("D"),
            targetPurchasePayableId.ToString("D"),
            amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

// ── Mapping (compartido con ReverseSupplierCreditApplicationUseCases) ────

internal static class Map
{
    public static SupplierCreditDto ToDto(Domain.Modules.Purchases.Entities.SupplierCredit c) =>
        new(
            c.Id,
            c.SupplierId,
            c.BranchId,
            c.CurrencyCode,
            c.SourcePurchaseReturnId,
            c.OriginalAmount,
            c.AvailableAmount,
            c.IsOpen,
            c.Movements.Select(m => new SupplierCreditMovementDto(
                    m.Id,
                    m.MovementType.ToString(),
                    m.Amount,
                    m.TargetPurchasePayableId,
                    m.ReversalOfMovementId,
                    m.CreatedAtUtc
                ))
                .ToList()
        );
}
