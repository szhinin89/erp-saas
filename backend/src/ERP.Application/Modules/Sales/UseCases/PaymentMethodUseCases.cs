using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record PaymentMethodDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    bool RequiresReference,
    bool IsCreditAllowed,
    int SortOrder,
    PaymentMethodDetailType DetailType,
    string? SriPaymentMethodCode,
    /// <summary>
    /// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — cuenta contable configurada para este método de
    /// pago en la Company activa (null si no se ha configurado ninguna — un método no-Crédito sin
    /// esta cuenta bloquea la autorización de cualquier venta que lo use, ver
    /// AuthorizeSalesInvoiceHandler). Sin significado para métodos <c>IsCreditAllowed = true</c>
    /// (Crédito no mueve Caja/Bancos).
    /// </summary>
    Guid? AccountingAccountId
);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetPaymentMethodsQuery(bool OnlyActive = true)
    : IRequest<Result<IReadOnlyList<PaymentMethodDto>>>,
        ICompanyScopedRequest;

public sealed record GetPaymentMethodByIdQuery(Guid Id)
    : IRequest<Result<PaymentMethodDto>>,
        ICompanyScopedRequest;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreatePaymentMethodCommand(
    string Code,
    string Name,
    bool RequiresReference = false,
    bool IsCreditAllowed = false,
    int SortOrder = 0,
    PaymentMethodDetailType DetailType = PaymentMethodDetailType.None,
    string? SriPaymentMethodCode = null
) : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

public sealed record UpdatePaymentMethodCommand(
    Guid Id,
    string Name,
    bool RequiresReference,
    bool IsCreditAllowed,
    int SortOrder,
    PaymentMethodDetailType DetailType,
    string? SriPaymentMethodCode = null
) : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

public sealed record TogglePaymentMethodCommand(Guid Id)
    : IRequest<Result<PaymentMethodDto>>,
        ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

/// <summary>
/// SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: SriPaymentMethodCode se valida de forma async contra
/// el catálogo real <c>global.sri_payment_method</c> (mismo patrón que
/// UpdateSupplierRoleConfigValidator) — nunca un HashSet fijo de códigos en Domain/Application.
/// </summary>
public sealed class CreatePaymentMethodValidator : AbstractValidator<CreatePaymentMethodCommand>
{
    public CreatePaymentMethodValidator(ISriCatalogLookupRepository catalogRepo)
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(PaymentMethod.MaxCodeLength)
            .WithMessage("El código es obligatorio (máx 20 caracteres).");
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(PaymentMethod.MaxNameLength)
            .WithMessage("El nombre es obligatorio (máx 100 caracteres).");
        RuleFor(x => x.SriPaymentMethodCode)
            .MustAsync((v, ct) => catalogRepo.PaymentMethodCodeExistsActiveAsync(v!, ct))
            .WithMessage(
                "SriPaymentMethodCode no corresponde a un código activo del catálogo sri_payment_method."
            )
            .When(x => x.SriPaymentMethodCode is not null);
    }
}

public sealed class UpdatePaymentMethodValidator : AbstractValidator<UpdatePaymentMethodCommand>
{
    public UpdatePaymentMethodValidator(ISriCatalogLookupRepository catalogRepo)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PaymentMethod.MaxNameLength);
        RuleFor(x => x.SriPaymentMethodCode)
            .MustAsync((v, ct) => catalogRepo.PaymentMethodCodeExistsActiveAsync(v!, ct))
            .WithMessage(
                "SriPaymentMethodCode no corresponde a un código activo del catálogo sri_payment_method."
            )
            .When(x => x.SriPaymentMethodCode is not null);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPaymentMethodsHandler
    : IRequestHandler<GetPaymentMethodsQuery, Result<IReadOnlyList<PaymentMethodDto>>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly IPaymentMethodAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPaymentMethodsHandler(
        IPaymentMethodRepository repo,
        IPaymentMethodAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
    }

    public async Task<Result<IReadOnlyList<PaymentMethodDto>>> Handle(
        GetPaymentMethodsQuery q,
        CancellationToken ct
    )
    {
        var list = await _repo.ListAsync(_t.TenantId, q.OnlyActive, ct);
        var accountMap = await _accountRepo.GetMapAsync(_t.TenantId, _c.CompanyId, ct);
        var dtos = list.Select(pm => ToDto(pm, accountMap)).ToList();
        return Result<IReadOnlyList<PaymentMethodDto>>.Success(dtos);
    }

    internal static PaymentMethodDto ToDto(
        PaymentMethod pm,
        IReadOnlyDictionary<Guid, Domain.Modules.Sales.Entities.PaymentMethodAccount>? accountMap = null
    ) =>
        new(
            pm.Id,
            pm.Code,
            pm.Name,
            pm.IsActive,
            pm.RequiresReference,
            pm.IsCreditAllowed,
            pm.SortOrder,
            pm.DetailType,
            pm.SriPaymentMethodCode,
            accountMap is not null && accountMap.TryGetValue(pm.Id, out var link)
                ? link.AccountingAccountId
                : null
        );
}

public sealed class GetPaymentMethodByIdHandler
    : IRequestHandler<GetPaymentMethodByIdQuery, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly IPaymentMethodAccountRepository _accountRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;

    public GetPaymentMethodByIdHandler(
        IPaymentMethodRepository repo,
        IPaymentMethodAccountRepository accountRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        _repo = repo;
        _accountRepo = accountRepo;
        _t = t;
        _c = c;
    }

    public async Task<Result<PaymentMethodDto>> Handle(
        GetPaymentMethodByIdQuery q,
        CancellationToken ct
    )
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (pm is null)
            return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        var link = await _accountRepo.GetAsync(_t.TenantId, _c.CompanyId, pm.Id, ct);
        return Result<PaymentMethodDto>.Success(
            GetPaymentMethodsHandler.ToDto(
                pm,
                link is null
                    ? null
                    : new Dictionary<Guid, Domain.Modules.Sales.Entities.PaymentMethodAccount>
                    {
                        [pm.Id] = link,
                    }
            )
        );
    }
}

public sealed class CreatePaymentMethodHandler
    : IRequestHandler<CreatePaymentMethodCommand, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CreatePaymentMethodHandler(
        IPaymentMethodRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PaymentMethodDto>> Handle(
        CreatePaymentMethodCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        if (await _repo.ExistsByCodeAsync(tid, cmd.Code, null, ct))
            return Result<PaymentMethodDto>.ValidationFailure(
                $"Ya existe un método de pago con código '{cmd.Code}'."
            );

        var pm = PaymentMethod.Create(
            tid,
            cmd.Code,
            cmd.Name,
            cmd.RequiresReference,
            cmd.IsCreditAllowed,
            cmd.SortOrder,
            _u.UserId,
            cmd.DetailType,
            cmd.SriPaymentMethodCode
        );

        await _repo.AddAsync(pm, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<PaymentMethodDto>.Success(GetPaymentMethodsHandler.ToDto(pm));
    }
}

public sealed class UpdatePaymentMethodHandler
    : IRequestHandler<UpdatePaymentMethodCommand, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdatePaymentMethodHandler(
        IPaymentMethodRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PaymentMethodDto>> Handle(
        UpdatePaymentMethodCommand cmd,
        CancellationToken ct
    )
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pm is null)
            return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        pm.Update(
            cmd.Name,
            cmd.RequiresReference,
            cmd.IsCreditAllowed,
            cmd.SortOrder,
            _u.UserId,
            cmd.DetailType,
            cmd.SriPaymentMethodCode
        );
        await _repo.SaveChangesAsync(ct);
        return Result<PaymentMethodDto>.Success(GetPaymentMethodsHandler.ToDto(pm));
    }
}

public sealed class TogglePaymentMethodHandler
    : IRequestHandler<TogglePaymentMethodCommand, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public TogglePaymentMethodHandler(
        IPaymentMethodRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PaymentMethodDto>> Handle(
        TogglePaymentMethodCommand cmd,
        CancellationToken ct
    )
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pm is null)
            return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        if (pm.IsActive)
            pm.Disable(_u.UserId);
        else
            pm.Enable(_u.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<PaymentMethodDto>.Success(GetPaymentMethodsHandler.ToDto(pm));
    }
}

// ── SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 ──────────────────────────

public sealed record SetPaymentMethodAccountCommand(Guid PaymentMethodId, Guid AccountingAccountId)
    : IRequest<Result<PaymentMethodDto>>,
        ICompanyScopedRequest;

/// <summary>
/// Valida que la cuenta exista, pertenezca a la Company activa, esté activa y admita movimiento —
/// mismo criterio de <c>PostingAccountGuard</c> (tiempo de posting), aplicado aquí en tiempo de
/// configuración para no permitir guardar una cuenta que de todos modos rechazaría toda venta.
/// </summary>
public sealed class SetPaymentMethodAccountValidator
    : AbstractValidator<SetPaymentMethodAccountCommand>
{
    public SetPaymentMethodAccountValidator(
        IAccountRepository accountRepo,
        IPaymentMethodRepository paymentMethodRepo,
        ICurrentTenant t,
        ICurrentCompany c
    )
    {
        RuleFor(x => x.PaymentMethodId)
            .MustAsync(
                async (id, ct) => await paymentMethodRepo.GetByIdAsync(t.TenantId, id, ct) is not null
            )
            .WithMessage("Método de pago no encontrado.");

        RuleFor(x => x.AccountingAccountId)
            .MustAsync(
                async (id, ct) =>
                {
                    var account = await accountRepo.GetByIdAsync(t.TenantId, c.CompanyId, id, ct);
                    return account is { IsActive: true, AllowsPosting: true };
                }
            )
            .WithMessage(
                "La cuenta contable seleccionada no existe, no pertenece a esta empresa, está "
                    + "inactiva o no admite movimientos."
            );
    }
}

public sealed class SetPaymentMethodAccountHandler
    : IRequestHandler<SetPaymentMethodAccountCommand, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly IPaymentMethodAccountRepository _accountLinkRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public SetPaymentMethodAccountHandler(
        IPaymentMethodRepository repo,
        IPaymentMethodAccountRepository accountLinkRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _accountLinkRepo = accountLinkRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PaymentMethodDto>> Handle(
        SetPaymentMethodAccountCommand cmd,
        CancellationToken ct
    )
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, cmd.PaymentMethodId, ct);
        if (pm is null)
            return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        var existing = await _accountLinkRepo.GetAsync(_t.TenantId, _c.CompanyId, pm.Id, ct);
        Domain.Modules.Sales.Entities.PaymentMethodAccount link;
        if (existing is null)
        {
            link = Domain.Modules.Sales.Entities.PaymentMethodAccount.Create(
                _t.TenantId,
                _c.CompanyId,
                pm.Id,
                cmd.AccountingAccountId,
                _u.UserId
            );
            await _accountLinkRepo.AddAsync(link, ct);
        }
        else
        {
            existing.ChangeAccount(cmd.AccountingAccountId, _u.UserId);
            link = existing;
        }
        await _accountLinkRepo.SaveChangesAsync(ct);

        return Result<PaymentMethodDto>.Success(
            GetPaymentMethodsHandler.ToDto(
                pm,
                new Dictionary<Guid, Domain.Modules.Sales.Entities.PaymentMethodAccount>
                {
                    [pm.Id] = link,
                }
            )
        );
    }
}
