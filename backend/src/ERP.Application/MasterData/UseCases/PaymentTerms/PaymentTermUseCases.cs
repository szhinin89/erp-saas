using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.MasterData.UseCases.PaymentTerms;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record PaymentTermDto(
    Guid   Id,
    string Code,
    string Name,
    int    Installments,
    int    DaysBetweenInstallments,
    int    TotalDays,
    string Summary,
    bool   IsActive)
{
    public static PaymentTermDto From(PaymentTerm e) => new(
        e.Id, e.Code, e.Name, e.Installments, e.DaysBetweenInstallments,
        e.TotalDays, e.Summary, e.IsActive);
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record ListPaymentTermsQuery(string? Search = null)
    : IRequest<IReadOnlyList<PaymentTermDto>>, ITenantScopedRequest;

public sealed record GetPaymentTermByIdQuery(Guid Id)
    : IRequest<Result<PaymentTermDto>>, ITenantScopedRequest;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreatePaymentTermCommand(
    string Code,
    string Name,
    int    Installments,
    int    DaysBetweenInstallments)
    : IRequest<Result<PaymentTermDto>>, ITenantScopedRequest;

public sealed record UpdatePaymentTermCommand(
    Guid   Id,
    string Name,
    int    Installments,
    int    DaysBetweenInstallments)
    : IRequest<Result<PaymentTermDto>>, ITenantScopedRequest;

public sealed record EnablePaymentTermCommand(Guid Id)
    : IRequest<Result<bool>>, ITenantScopedRequest;

public sealed record DisablePaymentTermCommand(Guid Id)
    : IRequest<Result<bool>>, ITenantScopedRequest;

// ── Validators ───────────────────────────────────────────────────────────────

public sealed class CreatePaymentTermValidator : AbstractValidator<CreatePaymentTermCommand>
{
    public CreatePaymentTermValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(PaymentTerm.MaxCodeLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PaymentTerm.MaxNameLength);
        RuleFor(x => x.Installments).InclusiveBetween(1, PaymentTerm.MaxInstallments);
        RuleFor(x => x.DaysBetweenInstallments).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdatePaymentTermValidator : AbstractValidator<UpdatePaymentTermCommand>
{
    public UpdatePaymentTermValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(PaymentTerm.MaxNameLength);
        RuleFor(x => x.Installments).InclusiveBetween(1, PaymentTerm.MaxInstallments);
        RuleFor(x => x.DaysBetweenInstallments).GreaterThanOrEqualTo(0);
    }
}

// ── Handlers ─────────────────────────────────────────────────────────────────

file sealed class ListHandler(IPaymentTermRepository repo, ICurrentTenant tenant)
    : IRequestHandler<ListPaymentTermsQuery, IReadOnlyList<PaymentTermDto>>
{
    public async Task<IReadOnlyList<PaymentTermDto>> Handle(ListPaymentTermsQuery request, CancellationToken ct)
    {
        var list = await repo.ListAsync(tenant.TenantId, request.Search, ct);
        return list.Select(PaymentTermDto.From).ToList();
    }
}

file sealed class GetByIdHandler(IPaymentTermRepository repo, ICurrentTenant tenant)
    : IRequestHandler<GetPaymentTermByIdQuery, Result<PaymentTermDto>>
{
    public async Task<Result<PaymentTermDto>> Handle(GetPaymentTermByIdQuery request, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, request.Id, ct);
        return entity is null
            ? Result<PaymentTermDto>.NotFound("Condición de pago no encontrada.")
            : Result<PaymentTermDto>.Success(PaymentTermDto.From(entity));
    }
}

file sealed class CreateHandler(
    IPaymentTermRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow)
    : IRequestHandler<CreatePaymentTermCommand, Result<PaymentTermDto>>
{
    public async Task<Result<PaymentTermDto>> Handle(CreatePaymentTermCommand cmd, CancellationToken ct)
    {
        if (await repo.ExistsByCodeAsync(tenant.TenantId, cmd.Code, cancellationToken: ct))
            return Result<PaymentTermDto>.Conflict($"Ya existe una condición de pago con código '{cmd.Code.Trim().ToUpperInvariant()}'.");

        var entity = PaymentTerm.Create(
            tenant.TenantId, cmd.Code, cmd.Name,
            cmd.Installments, cmd.DaysBetweenInstallments, user.UserId);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Result<PaymentTermDto>.Success(PaymentTermDto.From(entity));
    }
}

file sealed class UpdateHandler(
    IPaymentTermRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow)
    : IRequestHandler<UpdatePaymentTermCommand, Result<PaymentTermDto>>
{
    public async Task<Result<PaymentTermDto>> Handle(UpdatePaymentTermCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null) return Result<PaymentTermDto>.NotFound("Condición de pago no encontrada.");

        entity.Update(cmd.Name, cmd.Installments, cmd.DaysBetweenInstallments, user.UserId);
        repo.Update(entity);
        await uow.SaveChangesAsync(ct);
        return Result<PaymentTermDto>.Success(PaymentTermDto.From(entity));
    }
}

file sealed class EnableHandler(IPaymentTermRepository repo, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork uow)
    : IRequestHandler<EnablePaymentTermCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(EnablePaymentTermCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null) return Result<bool>.NotFound("Condición de pago no encontrada.");
        entity.Enable(user.UserId);
        await uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

file sealed class DisableHandler(IPaymentTermRepository repo, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork uow)
    : IRequestHandler<DisablePaymentTermCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DisablePaymentTermCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null) return Result<bool>.NotFound("Condición de pago no encontrada.");
        entity.Disable(user.UserId);
        await uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
