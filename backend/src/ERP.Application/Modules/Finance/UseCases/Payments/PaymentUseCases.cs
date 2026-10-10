using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.Payments;

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// Fase 5.5.5.3 — registra un cobro (AR) aplicándolo contra una o más <c>SalesReceivable</c>.
/// Toda la lógica de negocio (balance, límites de saldo, transiciones de estado) vive en
/// <c>Payment</c>/<c>SalesReceivable</c> — este handler solo orquesta: carga, delega, persiste.
/// </summary>
public sealed record RegisterCollectionCommand(
    Guid CustomerId,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? PaymentMethodId,
    string? Reference,
    IReadOnlyList<PaymentApplicationLineInput> Lines,
    /// <summary>
    /// Cuenta bancaria que recibió el cobro. Opcional: sin especificar, la contabilización sigue
    /// usando la cuenta fija de la PostingRule (comportamiento previo, sin cambios). Mutuamente
    /// excluyente con <see cref="CashRegisterId"/>.
    /// </summary>
    Guid? CompanyBankAccountId = null,
    /// <summary>Caja que recibió el cobro — mutuamente excluyente con <see cref="CompanyBankAccountId"/>.</summary>
    Guid? CashRegisterId = null,
    /// <summary>
    /// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — intención del cliente (una por cobro que el usuario
    /// quiere registrar; estable en reintentos). Obligatorio.
    /// </summary>
    Guid ClientRequestId = default
) : IRequest<Result<PaymentDto>>, ICompanyScopedRequest;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — representación canónica V1 de la intención de un cobro
/// (solo datos del usuario; el contexto tenant/empresa/actor no participa). Cambiar su forma = V2.
/// </summary>
internal sealed record CollectionIntentV1(
    Guid CustomerId,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? PaymentMethodId,
    string? Reference,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    IReadOnlyList<CollectionIntentLineV1> Lines
)
{
    public static string ComputeHash(RegisterCollectionCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CollectionIntentV1(
                cmd.CustomerId,
                cmd.Amount,
                cmd.PaymentDate,
                cmd.PaymentMethodId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                cmd.CompanyBankAccountId,
                cmd.CashRegisterId,
                cmd.Lines.Select(l => new CollectionIntentLineV1(
                        l.DocumentId,
                        l.InstallmentId,
                        l.AppliedAmount
                    ))
                    .ToList()
            )
        );
}

internal sealed record CollectionIntentLineV1(
    Guid DocumentId,
    Guid? InstallmentId,
    decimal AppliedAmount
);

/// <summary>Fase 5.5.5.3 — reversa un cobro ya aplicado y decrementa el saldo de cada CxC afectada.</summary>
public sealed record ReverseCollectionCommand(Guid PaymentId, string Reason)
    : IRequest<Result<PaymentDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class PaymentApplicationLineInputValidator
    : AbstractValidator<PaymentApplicationLineInput>
{
    public PaymentApplicationLineInputValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.AppliedAmount).GreaterThan(0);
    }
}

public sealed class RegisterCollectionCommandValidator
    : AbstractValidator<RegisterCollectionCommand>
{
    public RegisterCollectionCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("El cobro debe tener al menos una línea de aplicación.");
        RuleForEach(x => x.Lines).SetValidator(new PaymentApplicationLineInputValidator());
        RuleFor(x => x)
            .Must(x => x.CompanyBankAccountId is null || x.CashRegisterId is null)
            .WithMessage("Un cobro no puede tener cuenta bancaria y caja destino a la vez.");
    }
}

public sealed class ReverseCollectionCommandValidator : AbstractValidator<ReverseCollectionCommand>
{
    public ReverseCollectionCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del reverso es obligatorio.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

/// <remarks>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — una intención (<see cref="RegisterCollectionCommand.ClientRequestId"/>)
/// produce como máximo un cobro: mismo id + misma huella → el cobro ya registrado (sin volver a
/// aplicar la CxC, postear ni encolar eventos); mismo id + otra huella → Conflict. La barrera
/// definitiva es el índice único (TenantId, ClientRequestId) dentro del mismo SaveChanges atómico
/// que persiste cobro, CxC, asiento y outbox. Si el intento falla por cualquier motivo y la
/// intención quedó registrada por un reintento concurrente, se responde con el cobro ganador.
/// <para>
/// ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01 — cobros DISTINTOS sobre la misma CxC se serializan:
/// transacción explícita → CxC FOR UPDATE (orden por Id) → saldo recargado bajo el lock → recién
/// entonces se decide si cada monto cabe. El perdedor reevalúa contra el saldo ya consumido y se
/// rechaza con la regla de dominio canónica (sin ningún efecto). Orden de locks: CxC → secuencia
/// de asientos (posting en SaveChanges). El cobro no toma CashSession (no crea CashMovement).
/// </para>
/// </remarks>
public sealed class RegisterCollectionCommandHandler
    : IRequestHandler<RegisterCollectionCommand, Result<PaymentDto>>
{
    internal const string ClientRequestConflict =
        "Ya existe un cobro con este identificador pero con datos distintos.";

    private readonly IPaymentRepository _payments;
    private readonly ISalesReceivableRepository _receivables;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public RegisterCollectionCommandHandler(
        IPaymentRepository payments,
        ISalesReceivableRepository receivables,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _receivables = receivables;
        _bankAccounts = bankAccounts;
        _cashRegisters = cashRegisters;
        _uow = uow;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(
        RegisterCollectionCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;
        var key = new ClientRequestKey(cmd.ClientRequestId, CollectionIntentV1.ComputeHash(cmd));

        // Camino rápido del replay: ningún efecto.
        var existing = await _payments.GetByClientRequestIdAsync(tenantId, companyId, key.Id, ct);
        if (existing is not null)
            return Replay(existing, key);

        await _uow.BeginTransactionAsync(ct);
        Result<PaymentDto> result;
        try
        {
            result = await RegisterAsync(cmd, key, tenantId, companyId, ct);
            if (result.IsSuccess)
                await _uow.CommitAsync(ct);
            else
                await _uow.RollbackAsync(ct);
        }
        catch
        {
            // El intento perdedor se revierte completo (cobro, CxC, asiento, outbox) y libera los
            // locks; si la misma intención ya quedó registrada, se responde con ella.
            await _uow.RollbackAsync(ct);
            var raced = await _payments.GetByClientRequestIdAsync(tenantId, companyId, key.Id, ct);
            if (raced is null)
                throw;
            return Replay(raced, key);
        }
        if (result.IsSuccess)
            return result;

        var winner = await _payments.GetByClientRequestIdAsync(tenantId, companyId, key.Id, ct);
        return winner is not null ? Replay(winner, key) : result;
    }

    private async Task<Result<PaymentDto>> RegisterAsync(
        RegisterCollectionCommand cmd,
        ClientRequestKey key,
        Guid tenantId,
        Guid companyId,
        CancellationToken ct
    )
    {
        // Una cuenta bancaria o caja explícitamente elegida debe existir, pertenecer a esta
        // empresa, estar activa y tener cuenta contable configurada (a diferencia del caso "sin
        // especificar", que nunca bloquea el cobro — ver PostingRule fallback en el traductor).
        if (cmd.CompanyBankAccountId is { } bankAccountId)
        {
            var bankAccount = await _bankAccounts.GetByIdAsync(tenantId, bankAccountId, ct);
            if (bankAccount is null || bankAccount.CompanyId != companyId || !bankAccount.IsActive)
                return Result<PaymentDto>.ValidationFailure(
                    "La cuenta bancaria indicada no existe, no pertenece a esta empresa o está inactiva."
                );
        }
        if (cmd.CashRegisterId is { } cashRegisterId)
        {
            var cashRegister = await _cashRegisters.GetByIdAsync(tenantId, cashRegisterId, ct);
            if (
                cashRegister is null
                || cashRegister.CompanyId != companyId
                || !cashRegister.IsActive
            )
                return Result<PaymentDto>.ValidationFailure(
                    "La caja indicada no existe, no pertenece a esta empresa o está inactiva."
                );
            if (cashRegister.AccountingAccountId is null)
                return Result<PaymentDto>.ValidationFailure(
                    "La caja indicada no tiene una cuenta contable configurada. Configúrela en Cajas registradoras antes de registrar el cobro."
                );
        }

        Payment payment;
        try
        {
            payment = Payment.Create(
                tenantId,
                companyId,
                PaymentDirection.Collection,
                cmd.CustomerId,
                cmd.Amount,
                cmd.PaymentDate,
                cmd.PaymentMethodId,
                cmd.Reference,
                _u.UserId,
                cmd.CompanyBankAccountId,
                cmd.CashRegisterId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }
        payment.BindClientRequest(key);

        // Cada CxC referenciada se bloquea una sola vez (orden por Id) y se recarga bajo el lock
        // ANTES de decidir si el monto cabe: un cobro concurrente distinto espera y reevalúa
        // contra el saldo ya consumido. Una CxC fuera del alcance operativo no se devuelve.
        var receivablesByDocId = await _receivables.GetByIdsForUpdateAsync(
            tenantId,
            cmd.Lines.Select(l => l.DocumentId).ToList(),
            ct
        );
        foreach (var line in cmd.Lines)
        {
            if (!receivablesByDocId.TryGetValue(line.DocumentId, out var receivable))
                return Result<PaymentDto>.NotFound(
                    $"Cuenta por cobrar {line.DocumentId} no encontrada."
                );

            // ZH-COLLECTIONS-CUSTOMER-SCOPE-01 — validado ya bajo lock y antes de modificar nada: un
            // cobro de un cliente nunca reduce la CxC de otro (el saldo bajaría en B mientras el
            // pago, sus eventos y el asiento quedan a nombre de A), y una cuota informada debe ser
            // de esa misma CxC. No se confía en el cliente: cualquier línea inválida rechaza el
            // cobro completo y Handle revierte la transacción.
            if (receivable.CustomerId != cmd.CustomerId)
                return Result<PaymentDto>.ValidationFailure(
                    $"La cuenta por cobrar {line.DocumentId} no pertenece al cliente del cobro."
                );
            if (
                line.InstallmentId is { } installmentId
                && receivable.Installments.All(i => i.Id != installmentId)
            )
                return Result<PaymentDto>.ValidationFailure(
                    $"La cuota {installmentId} no pertenece a la cuenta por cobrar {line.DocumentId}."
                );

            try
            {
                payment.AddApplicationLine(line.DocumentId, line.InstallmentId, line.AppliedAmount);
            }
            catch (ArgumentException ex)
            {
                return Result<PaymentDto>.ValidationFailure(ex.Message);
            }
        }

        payment.Apply(_u.UserId);

        foreach (var line in cmd.Lines)
        {
            receivablesByDocId[line.DocumentId].RegisterCollection(line.AppliedAmount, _u.UserId);
        }

        await _payments.AddAsync(payment, ct);
        await _payments.SaveChangesAsync(ct);

        return Result<PaymentDto>.Success(Map.ToDto(payment));
    }

    /// <summary>Mismo request → el cobro ya registrado; request distinto → Conflict.</summary>
    private static Result<PaymentDto> Replay(Payment existing, ClientRequestKey key) =>
        key.Matches(existing.RequestPayloadHash)
            ? Result<PaymentDto>.Success(Map.ToDto(existing))
            : Result<PaymentDto>.Conflict(ClientRequestConflict);
}

public sealed class ReverseCollectionCommandHandler
    : IRequestHandler<ReverseCollectionCommand, Result<PaymentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly ISalesReceivableRepository _receivables;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReverseCollectionCommandHandler(
        IPaymentRepository payments,
        ISalesReceivableRepository receivables,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _payments = payments;
        _receivables = receivables;
        _uow = uow;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentDto>> Handle(ReverseCollectionCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var result = await ReverseAsync(cmd, ct);
            if (result.IsSuccess)
                await _uow.CommitAsync(ct);
            else
                await _uow.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Result<PaymentDto>> ReverseAsync(
        ReverseCollectionCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        // ZH-COLLECTIONS-RECEIVABLE-CONCURRENCY-01 — mismo orden de locks que el registro:
        // pago → CxC (por Id) → secuencia de asientos. Dos reversas del mismo cobro se serializan
        // en el pago; la segunda lo ve ya reversado y se rechaza por la regla de dominio.
        var payment = await _payments.GetByIdForUpdateAsync(tenantId, companyId, cmd.PaymentId, ct);
        if (payment is null)
            return Result<PaymentDto>.NotFound("Pago no encontrado.");
        if (payment.Direction != PaymentDirection.Collection)
            return Result<PaymentDto>.ValidationFailure("El pago indicado no es un cobro.");

        try
        {
            payment.Reverse(_u.UserId, cmd.Reason);
        }
        catch (ArgumentException ex)
        {
            return Result<PaymentDto>.ValidationFailure(ex.Message);
        }

        var receivables = await _receivables.GetByIdsForUpdateAsync(
            tenantId,
            payment.Lines.Select(l => l.ReceivableId!.Value).ToList(),
            ct
        );
        foreach (var line in payment.Lines)
        {
            if (!receivables.TryGetValue(line.ReceivableId!.Value, out var receivable))
                return Result<PaymentDto>.NotFound(
                    $"Cuenta por cobrar {line.ReceivableId} no encontrada."
                );

            receivable.ReverseCollection(line.AppliedAmount, _u.UserId);
        }

        await _payments.SaveChangesAsync(ct);
        return Result<PaymentDto>.Success(Map.ToDto(payment));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static PaymentDto ToDto(Payment p) =>
        new(
            p.Id,
            p.Direction.ToString(),
            p.PartnerId,
            p.Amount,
            p.PaymentDate,
            p.PaymentMethodId,
            p.Reference,
            p.Status.ToString(),
            p.AppliedAtUtc,
            p.ReversedAtUtc,
            p.ReverseReason,
            p.Lines.Select(l => new PaymentApplicationLineDto(
                    l.Id,
                    l.ReceivableId,
                    l.PayableId,
                    l.InstallmentId,
                    l.AppliedAmount
                ))
                .ToList(),
            p.CreatedAt,
            p.UpdatedAt,
            p.CompanyBankAccountId,
            p.CashRegisterId
        );
}
