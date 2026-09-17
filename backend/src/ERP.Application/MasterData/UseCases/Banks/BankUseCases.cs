using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.MasterData.UseCases.Banks;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record BankDto(
    Guid Id,
    string CountryCode,
    string Code,
    string Name,
    string? ShortName,
    bool IsActive
)
{
    public static BankDto From(Bank e) =>
        new(e.Id, e.CountryCode, e.Code, e.Name, e.ShortName, e.IsActive);
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record ListBanksQuery(bool OnlyActive = false, string? Search = null)
    : IRequest<IReadOnlyList<BankDto>>,
        ITenantScopedRequest;

public sealed record GetBankByIdQuery(Guid Id) : IRequest<Result<BankDto>>, ITenantScopedRequest;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record CreateBankCommand(
    string Code,
    string Name,
    string? ShortName,
    string CountryCode = Bank.DefaultCountryCode
) : IRequest<Result<BankDto>>, ITenantScopedRequest;

public sealed record UpdateBankCommand(Guid Id, string Name, string? ShortName)
    : IRequest<Result<BankDto>>,
        ITenantScopedRequest;

public sealed record EnableBankCommand(Guid Id) : IRequest<Result<bool>>, ITenantScopedRequest;

public sealed record DisableBankCommand(Guid Id) : IRequest<Result<bool>>, ITenantScopedRequest;

// ── Validators ───────────────────────────────────────────────────────────────

public sealed class CreateBankValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(Bank.MaxCountryCodeLength);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Bank.MaxCodeLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Bank.MaxNameLength);
        RuleFor(x => x.ShortName).MaximumLength(Bank.MaxShortNameLength);
    }
}

public sealed class UpdateBankValidator : AbstractValidator<UpdateBankCommand>
{
    public UpdateBankValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Bank.MaxNameLength);
        RuleFor(x => x.ShortName).MaximumLength(Bank.MaxShortNameLength);
    }
}

// ── Handlers ─────────────────────────────────────────────────────────────────

file sealed class ListHandler(IBankRepository repo, ICurrentTenant tenant)
    : IRequestHandler<ListBanksQuery, IReadOnlyList<BankDto>>
{
    public async Task<IReadOnlyList<BankDto>> Handle(ListBanksQuery request, CancellationToken ct)
    {
        var list = await repo.ListAsync(tenant.TenantId, request.OnlyActive, request.Search, ct);
        return list.Select(BankDto.From).ToList();
    }
}

file sealed class GetByIdHandler(IBankRepository repo, ICurrentTenant tenant)
    : IRequestHandler<GetBankByIdQuery, Result<BankDto>>
{
    public async Task<Result<BankDto>> Handle(GetBankByIdQuery request, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, request.Id, ct);
        return entity is null
            ? Result<BankDto>.NotFound("Banco no encontrado.")
            : Result<BankDto>.Success(BankDto.From(entity));
    }
}

file sealed class CreateHandler(
    IBankRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow
) : IRequestHandler<CreateBankCommand, Result<BankDto>>
{
    public async Task<Result<BankDto>> Handle(CreateBankCommand cmd, CancellationToken ct)
    {
        if (
            await repo.ExistsByCodeAsync(
                tenant.TenantId,
                cmd.CountryCode,
                cmd.Code,
                cancellationToken: ct
            )
        )
            return Result<BankDto>.Conflict(
                $"Ya existe un banco con código '{cmd.Code.Trim().ToUpperInvariant()}' para el país '{cmd.CountryCode.Trim().ToUpperInvariant()}'."
            );

        var entity = Bank.Create(
            tenant.TenantId,
            cmd.Code,
            cmd.Name,
            cmd.ShortName,
            user.UserId,
            cmd.CountryCode
        );

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Result<BankDto>.Success(BankDto.From(entity));
    }
}

file sealed class UpdateHandler(
    IBankRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow
) : IRequestHandler<UpdateBankCommand, Result<BankDto>>
{
    public async Task<Result<BankDto>> Handle(UpdateBankCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<BankDto>.NotFound("Banco no encontrado.");

        entity.Update(cmd.Name, cmd.ShortName, user.UserId);
        repo.Update(entity);
        await uow.SaveChangesAsync(ct);
        return Result<BankDto>.Success(BankDto.From(entity));
    }
}

file sealed class EnableHandler(
    IBankRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow
) : IRequestHandler<EnableBankCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(EnableBankCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<bool>.NotFound("Banco no encontrado.");
        entity.Enable(user.UserId);
        await uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

file sealed class DisableHandler(
    IBankRepository repo,
    ICurrentTenant tenant,
    ICurrentUser user,
    IUnitOfWork uow
) : IRequestHandler<DisableBankCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DisableBankCommand cmd, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(tenant.TenantId, cmd.Id, ct);
        if (entity is null)
            return Result<bool>.NotFound("Banco no encontrado.");
        entity.Disable(user.UserId);
        await uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
