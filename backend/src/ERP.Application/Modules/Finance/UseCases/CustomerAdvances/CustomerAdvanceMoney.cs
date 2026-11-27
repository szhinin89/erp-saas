using ERP.Application.Common;
using ERP.Application.Modules.Caja;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>Medio y destino elegidos para recibir o devolver dinero de un anticipo.</summary>
internal sealed record CustomerAdvanceMoneyTarget(
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string? Reference
);

/// <summary>
/// ADR-052 §9 — reglas comunes del dinero de un anticipo: medio admitido (TA-02), destino activo y de
/// la empresa y turno de caja del titular (TA-13). Orden de locks: destino <c>FOR SHARE</c> → cuenta
/// <c>FOR SHARE</c> → turno <c>FOR UPDATE</c>.
/// </summary>
internal sealed class CustomerAdvanceMoney(
    IPaymentMethodRepository paymentMethods,
    ICompanyBankAccountRepository bankAccounts,
    ICashRegisterRepository cashRegisters,
    ICashSessionRepository cashSessions,
    IAccountRepository accounts
)
{
    public const string MethodNotAllowed =
        "Los anticipos de clientes solo admiten efectivo o transferencia bancaria.";

    /// <summary>Valida el medio y bloquea el destino; null si ambos son utilizables.</summary>
    public async Task<Result<CustomerAdvanceDto>?> ValidateTargetAsync(
        Guid tenantId,
        Guid companyId,
        CustomerAdvanceMoneyTarget target,
        CancellationToken ct
    )
    {
        var method = await paymentMethods.GetByIdAsync(tenantId, target.PaymentMethodId, ct);
        if (method is null)
            return Result<CustomerAdvanceDto>.NotFound("El método de pago indicado no existe.");
        if (ValidatePaymentMethod(method, target) is { } methodError)
            return Result<CustomerAdvanceDto>.ValidationFailure(methodError);
        return await LockDestinationAsync(tenantId, companyId, target, ct);
    }

    /// <summary>
    /// Turno abierto de la caja, de la empresa y del usuario, bloqueado. Con
    /// <paramref name="outflow"/> exige además efectivo suficiente (sin sobregiro).
    /// </summary>
    public async Task<(CashSession? Session, Result<CustomerAdvanceDto>? Error)> LockSessionAsync(
        Guid tenantId,
        Guid companyId,
        Guid cashRegisterId,
        Guid userId,
        decimal? outflow,
        CancellationToken ct
    )
    {
        var session = await cashSessions.GetOpenByCashRegisterForUpdateAsync(
            tenantId,
            cashRegisterId,
            ct
        );
        if (session is null || session.CompanyId != companyId)
            return Rejected("No existe una sesión de caja activa para la caja indicada.");
        // TA-13 — solo el titular del turno mueve el efectivo de esa caja.
        if (!session.IsControlledBy(userId))
            return Rejected(CashSessionOwnership.RejectionMessage(session));
        if (
            outflow is { } amount
            && CashSessionAvailability.OverdraftMessage(session, amount) is { } overdraft
        )
            return Rejected(overdraft);
        return (session, null);

        static (CashSession?, Result<CustomerAdvanceDto>?) Rejected(string message) =>
            (null, Result<CustomerAdvanceDto>.ValidationFailure(message));
    }

    /// <summary>Nunca se trunca: si no cabe en Caja, se resuelve por el movimiento referenciado.</summary>
    public static string? VisibleReference(string? reference) =>
        reference is { Length: <= CashMovement.ReferenceNumberMaxLen } ? reference : null;

    /// <summary>Medio activo, coherente con su destino, permitido por TA-02 y con su referencia.</summary>
    private static string? ValidatePaymentMethod(
        PaymentMethod method,
        CustomerAdvanceMoneyTarget target
    )
    {
        if (!method.IsActive)
            return "El método de pago indicado no está activo.";
        var destinationError = PaymentMethodDestinationPolicy.Validate(
            method,
            hasBankAccount: target.CompanyBankAccountId is not null,
            hasCashRegister: target.CashRegisterId is not null
        );
        if (destinationError is not null)
            return destinationError;
        // TA-02 — la restricción vive en Anticipos; la política compartida no cambia.
        if (!method.AffectsPhysicalCash && method.DetailType != PaymentMethodDetailType.Transfer)
            return MethodNotAllowed;
        return method.RequiresReference && string.IsNullOrWhiteSpace(target.Reference)
            ? "El método de pago seleccionado exige una referencia externa."
            : null;
    }

    /// <summary>
    /// Bloquea y valida el destino y la pertenencia de su cuenta contable. Que la cuenta esté activa e
    /// imputable lo decide Contabilidad en cada intento (obligación Failed, accounting.md §19).
    /// </summary>
    private async Task<Result<CustomerAdvanceDto>?> LockDestinationAsync(
        Guid tenantId,
        Guid companyId,
        CustomerAdvanceMoneyTarget target,
        CancellationToken ct
    )
    {
        Guid? accountId;
        if (target.CompanyBankAccountId is { } bankAccountId)
        {
            var bank = await bankAccounts.GetByIdForShareAsync(tenantId, bankAccountId, ct);
            if (bank is null || bank.CompanyId != companyId)
                return Result<CustomerAdvanceDto>.NotFound(
                    "La cuenta bancaria indicada no existe."
                );
            if (!bank.IsActive)
                return Result<CustomerAdvanceDto>.ValidationFailure(
                    "La cuenta bancaria indicada no está activa."
                );
            accountId = bank.AccountingAccountId;
        }
        else
        {
            var register = await cashRegisters.GetByIdForShareAsync(
                tenantId,
                target.CashRegisterId!.Value,
                ct
            );
            if (register is null || register.CompanyId != companyId)
                return Result<CustomerAdvanceDto>.NotFound("La caja indicada no existe.");
            if (!register.IsActive)
                return Result<CustomerAdvanceDto>.ValidationFailure(
                    "La caja indicada no está activa."
                );
            accountId = register.AccountingAccountId;
        }

        return
            accountId is { } id
            && await accounts.GetByIdForShareAsync(tenantId, companyId, id, ct) is null
            ? Result<CustomerAdvanceDto>.ValidationFailure(
                "La cuenta contable del destino no pertenece a esta empresa."
            )
            : null;
    }
}
