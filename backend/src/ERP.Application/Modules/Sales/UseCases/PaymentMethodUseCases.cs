using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record PaymentMethodDto(
    Guid Id, string Code, string Name, bool IsActive,
    bool RequiresReference, bool IsCreditAllowed, int SortOrder,
    PaymentMethodDetailType DetailType);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetPaymentMethodsQuery(bool OnlyActive = true)
    : IRequest<Result<IReadOnlyList<PaymentMethodDto>>>, ICompanyScopedRequest;

public sealed record GetPaymentMethodByIdQuery(Guid Id)
    : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record CreatePaymentMethodCommand(
    string Code, string Name,
    bool RequiresReference = false, bool IsCreditAllowed = false,
    int SortOrder = 0, PaymentMethodDetailType DetailType = PaymentMethodDetailType.None)
    : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

public sealed record UpdatePaymentMethodCommand(
    Guid Id, string Name,
    bool RequiresReference, bool IsCreditAllowed,
    int SortOrder, PaymentMethodDetailType DetailType)
    : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

public sealed record TogglePaymentMethodCommand(Guid Id)
    : IRequest<Result<PaymentMethodDto>>, ICompanyScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreatePaymentMethodValidator : AbstractValidator<CreatePaymentMethodCommand>
{
    public CreatePaymentMethodValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(PaymentMethod.MaxCodeLength)
            .WithMessage("El código es obligatorio (máx 20 caracteres).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PaymentMethod.MaxNameLength)
            .WithMessage("El nombre es obligatorio (máx 100 caracteres).");
    }
}

public sealed class UpdatePaymentMethodValidator : AbstractValidator<UpdatePaymentMethodCommand>
{
    public UpdatePaymentMethodValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PaymentMethod.MaxNameLength);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetPaymentMethodsHandler
    : IRequestHandler<GetPaymentMethodsQuery, Result<IReadOnlyList<PaymentMethodDto>>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    public GetPaymentMethodsHandler(IPaymentMethodRepository repo, ICurrentTenant t) { _repo = repo; _t = t; }

    public async Task<Result<IReadOnlyList<PaymentMethodDto>>> Handle(
        GetPaymentMethodsQuery q, CancellationToken ct)
    {
        var list = await _repo.ListAsync(_t.TenantId, q.OnlyActive, ct);
        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<PaymentMethodDto>>.Success(dtos);
    }

    internal static PaymentMethodDto ToDto(PaymentMethod pm) => new(
        pm.Id, pm.Code, pm.Name, pm.IsActive,
        pm.RequiresReference, pm.IsCreditAllowed, pm.SortOrder, pm.DetailType);
}

public sealed class GetPaymentMethodByIdHandler
    : IRequestHandler<GetPaymentMethodByIdQuery, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    public GetPaymentMethodByIdHandler(IPaymentMethodRepository repo, ICurrentTenant t) { _repo = repo; _t = t; }

    public async Task<Result<PaymentMethodDto>> Handle(GetPaymentMethodByIdQuery q, CancellationToken ct)
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return pm is null
            ? Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.")
            : Result<PaymentMethodDto>.Success(GetPaymentMethodsHandler.ToDto(pm));
    }
}

public sealed class CreatePaymentMethodHandler
    : IRequestHandler<CreatePaymentMethodCommand, Result<PaymentMethodDto>>
{
    private readonly IPaymentMethodRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;
    public CreatePaymentMethodHandler(IPaymentMethodRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<PaymentMethodDto>> Handle(CreatePaymentMethodCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        if (await _repo.ExistsByCodeAsync(tid, cmd.Code, null, ct))
            return Result<PaymentMethodDto>.ValidationFailure($"Ya existe un método de pago con código '{cmd.Code}'.");

        var pm = PaymentMethod.Create(tid, cmd.Code, cmd.Name,
            cmd.RequiresReference, cmd.IsCreditAllowed, cmd.SortOrder, _u.UserId, cmd.DetailType);

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
    public UpdatePaymentMethodHandler(IPaymentMethodRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<PaymentMethodDto>> Handle(UpdatePaymentMethodCommand cmd, CancellationToken ct)
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pm is null) return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        pm.Update(cmd.Name, cmd.RequiresReference, cmd.IsCreditAllowed, cmd.SortOrder, _u.UserId, cmd.DetailType);
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
    public TogglePaymentMethodHandler(IPaymentMethodRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<PaymentMethodDto>> Handle(TogglePaymentMethodCommand cmd, CancellationToken ct)
    {
        var pm = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (pm is null) return Result<PaymentMethodDto>.NotFound("Método de pago no encontrado.");

        if (pm.IsActive) pm.Disable(_u.UserId); else pm.Enable(_u.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<PaymentMethodDto>.Success(GetPaymentMethodsHandler.ToDto(pm));
    }
}
