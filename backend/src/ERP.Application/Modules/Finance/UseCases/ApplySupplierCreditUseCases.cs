using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases;

// DTOs de lectura: SupplierCreditReadModel.cs (ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D).

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 7 — aplica un <c>SupplierCredit</c> existente contra una <c>AccountsPayable</c>
/// destino: Lock A (destino) → Lock B, en ese orden fijo (§15.4), idempotente (§16.2, fila
/// <c>ApplyToPayable</c>). ZH-SUPPLIER-CREDIT-APPLY-PAYABLES-02D-C: el destino puede ser una CxP de
/// Compra o de Gasto — resolución de lock/empresa/moneda en <see cref="SupplierCreditPayableTarget"/>.
/// El nombre <c>TargetPurchasePayableId</c> se conserva por compatibilidad de contrato (API/UI/BD).
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
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public ApplySupplierCreditHandler(
        ISupplierCreditRepository creditRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReturnRepository purchaseReturnRepo,
        ICompanyRepository companyRepo,
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
        _companyRepo = companyRepo;
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

        // §15.4: Lock A (del destino, según su origen) siempre antes de Lock B (del
        // SupplierCredit). Descubrimiento sin tracking del origen de la CxP — garantiza que la
        // recarga posterior (después del lock) sea la primera lectura tracking, genuinamente fresca.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var lockError = await SupplierCreditPayableTarget.AcquireOriginLockAsync(
                _payableRepo,
                _purchaseReturnRepo,
                tid,
                cmd.TargetPurchasePayableId,
                ct
            );
            if (lockError is not null)
            {
                await _uow.RollbackAsync(ct);
                // SC-002 — el destino no existe o su origen no admite saldos a favor.
                return Result<SupplierCreditDto>.ValidationFailure(lockError);
            }

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
            var ownershipError = SupplierCreditPayableTarget.ValidateOwnership(payable, credit);
            if (ownershipError is not null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.ValidationFailure(ownershipError);
            }
            if (payable.Status == AccountsPayableStatus.Cancelled)
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

            var targetCurrency = await SupplierCreditPayableTarget.ResolveCurrencyAsync(
                payable,
                _invoiceRepo,
                _companyRepo,
                tid,
                ct
            );
            if (targetCurrency is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierCreditDto>.NotFound(
                    "El documento asociado a la cuenta por pagar destino no fue encontrado."
                );
            }
            if (
                !string.Equals(
                    targetCurrency,
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

            credit.ApplyToPayable(
                cmd.TargetPurchasePayableId,
                cmd.Amount,
                uid,
                cmd.ClientRequestId,
                hash
            );
            payable.ApplySupplierCredit(cmd.Amount, uid);

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

/// <summary>Respuesta de los comandos: mismo mapeo único del read-model, sin enriquecimiento (la API
/// re-lee el detalle enriquecido tras el comando).</summary>
internal static class Map
{
    public static SupplierCreditDto ToDto(Domain.Modules.Purchases.Entities.SupplierCredit c) =>
        SupplierCreditReadModel.ToDetail(c, SupplierCreditReadContext.Empty);
}
