using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F3 (ADR-052) — registra el dinero recibido de un cliente sin factura, con
/// confirmación directa. Tenant, empresa, sucursal, moneda y actor salen del contexto: nunca del
/// cliente. Efectivo entra al turno abierto de la caja; la transferencia va contra la cuenta bancaria.
/// </summary>
public sealed record RegisterCustomerAdvanceCommand(
    Guid CustomerId,
    decimal Amount,
    DateOnly AdvanceDate,
    Guid PaymentMethodId,
    /// <summary>Cuenta bancaria que recibió la transferencia. Excluyente con la caja.</summary>
    Guid? CompanyBankAccountId = null,
    /// <summary>Caja que recibió el efectivo. Excluyente con la cuenta bancaria.</summary>
    Guid? CashRegisterId = null,
    string? Reference = null,
    string? Reason = null,
    /// <summary>Intención del cliente: una por anticipo, estable en reintentos. Obligatorio.</summary>
    Guid ClientRequestId = default
) : IRequest<Result<CustomerAdvanceDto>>, IBranchScopedRequest;

/// <summary>
/// Representación canónica V1 de la intención de registro (ADR-052 §5): incluye el tipo de operación
/// y solo datos del usuario. Cambiar su forma = V2.
/// </summary>
internal sealed record CustomerAdvanceRegistrationIntentV1(
    string Operation,
    Guid CustomerId,
    decimal Amount,
    DateOnly AdvanceDate,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string? Reference,
    string? Reason
)
{
    public static string ComputeHash(RegisterCustomerAdvanceCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new CustomerAdvanceRegistrationIntentV1(
                "CustomerAdvance.Register",
                cmd.CustomerId,
                cmd.Amount,
                cmd.AdvanceDate,
                cmd.PaymentMethodId,
                cmd.CompanyBankAccountId,
                cmd.CashRegisterId,
                CanonicalRequestFingerprint.NormalizeText(cmd.Reference),
                CanonicalRequestFingerprint.NormalizeText(cmd.Reason)
            )
        );
}

public sealed class RegisterCustomerAdvanceCommandValidator
    : AbstractValidator<RegisterCustomerAdvanceCommand>
{
    public RegisterCustomerAdvanceCommandValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .Must(amount => decimal.Round(amount, FiscalPrecision.TaxAmount) == amount)
            .WithMessage("El monto excede la precisión monetaria permitida.");
        RuleFor(x => x.AdvanceDate).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Reference).MaximumLength(CustomerAdvance.ExternalReferenceMaxLen);
        RuleFor(x => x.Reason).MaximumLength(CustomerAdvance.NoteMaxLen);
        RuleFor(x => x)
            .Must(x => x.CompanyBankAccountId is not null ^ x.CashRegisterId is not null)
            .WithMessage("Debe especificar exactamente una cuenta bancaria o una caja destino.");
    }
}

/// <remarks>
/// Una intención produce como máximo un anticipo (CA-INV-03): replay por tenant + empresa + clave
/// (misma huella → el original; distinta → 409) y, como barrera definitiva, los índices únicos de la
/// clave. Anticipo, movimiento, caja, auditoría, obligación contable y outbox se confirman en una sola
/// transacción (CA-INV-10). Orden de locks (backend.md): destino FOR SHARE → cuenta FOR SHARE →
/// turno FOR UPDATE → locks contables.
/// </remarks>
public sealed class RegisterCustomerAdvanceHandler
    : IRequestHandler<RegisterCustomerAdvanceCommand, Result<CustomerAdvanceDto>>
{
    private const string CashMovementDescription = "Anticipo de cliente";

    private readonly ICustomerAdvanceRepository _advances;
    private readonly IBusinessPartnerRepository _partners;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly CustomerAdvanceMoney _money;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly CustomerAdvanceIntentRunner _intent;
    private readonly ICompanyClock _clock;

    public RegisterCustomerAdvanceHandler(
        ICustomerAdvanceRepository advances,
        ICustomerAdvanceRefundTransactionRepository transactions,
        IBusinessPartnerRepository partners,
        IBusinessPartnerRoleRepository roles,
        IPaymentMethodRepository paymentMethods,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        ICashSessionRepository cashSessions,
        IAccountRepository accounts,
        ICompanyRepository companies,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyClock clock
    )
    {
        _advances = advances;
        _partners = partners;
        _roles = roles;
        _money = new(paymentMethods, bankAccounts, cashRegisters, cashSessions, accounts);
        _companies = companies;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _clock = clock;
        _intent = new(advances, transactions, partners, uow, dbEx, t);
    }

    public Task<Result<CustomerAdvanceDto>> Handle(
        RegisterCustomerAdvanceCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CustomerAdvanceRegistrationIntentV1.ComputeHash(cmd)
        );
        return _intent.ExecuteAsync(key, token => RegisterAsync(cmd, key, token), ct);
    }

    private async Task<Result<CustomerAdvanceDto>> RegisterAsync(
        RegisterCustomerAdvanceCommand cmd,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var company = await _companies.GetByIdAsync(companyId, ct);
        if (company is null)
            return Result<CustomerAdvanceDto>.NotFound("La empresa activa no fue encontrada.");

        // ADR-052 §17 — se evalúa después del replay: el dinero no puede recibirse en el futuro.
        var today = await _clock.TodayAsync(companyId, tenantId, ct);
        if (cmd.AdvanceDate > today)
            return Result<CustomerAdvanceDto>.ValidationFailure(
                $"La fecha del anticipo {cmd.AdvanceDate:yyyy-MM-dd} es posterior al día de negocio {today:yyyy-MM-dd}."
            );

        // El socio de negocio es del tenant (compartido por sus empresas); debe ser cliente activo.
        var customer = await _partners.GetByIdAsync(cmd.CustomerId, ct);
        if (customer is null || customer.TenantId != tenantId)
            return Result<CustomerAdvanceDto>.NotFound("Cliente no encontrado.");
        if (!customer.IsActive)
            return Result<CustomerAdvanceDto>.ValidationFailure(
                "El cliente se encuentra inactivo."
            );
        if (!await _roles.HasActiveRoleAsync(customer.Id, RoleType.Customer, ct))
            return Result<CustomerAdvanceDto>.ValidationFailure(
                "El socio de negocio no tiene rol de Cliente."
            );

        var target = new CustomerAdvanceMoneyTarget(
            cmd.PaymentMethodId,
            cmd.CompanyBankAccountId,
            cmd.CashRegisterId,
            cmd.Reference
        );
        if (await _money.ValidateTargetAsync(tenantId, companyId, target, ct) is { } targetError)
            return targetError;

        var (session, sessionError) = cmd.CashRegisterId is { } cashRegisterId
            ? await _money.LockSessionAsync(
                tenantId,
                companyId,
                cashRegisterId,
                _u.UserId,
                outflow: null,
                ct
            )
            : default;
        if (sessionError is not null)
            return sessionError;

        var advance = CustomerAdvance.Register(
            tenantId,
            companyId,
            _b.BranchId,
            customer.Id,
            company.CurrencyCode,
            cmd.Amount,
            cmd.AdvanceDate,
            cmd.PaymentMethodId,
            cmd.CompanyBankAccountId,
            cmd.CashRegisterId,
            _u.UserId,
            key,
            cmd.Reference,
            cmd.Reason
        );

        if (session is not null)
        {
            // Movimiento interno sin motivo: lo contabiliza el anticipo, nunca Caja.
            var received = advance.Movements[0];
            var cashMovement = session.RecordMovement(
                CashMovementType.ManualIncome,
                advance.OriginalAmount,
                CashMovementDescription,
                _u.UserId,
                CashReferenceType.CustomerAdvance,
                received.Id,
                CustomerAdvanceMoney.VisibleReference(advance.ExternalReference)
            );
            advance.LinkCashMovement(received.Id, session.Id, cashMovement.Id);
        }

        await _advances.AddAsync(advance, ct);
        await _advances.SaveChangesAsync(ct);

        return await _intent.SuccessAsync(advance, ct);
    }
}
