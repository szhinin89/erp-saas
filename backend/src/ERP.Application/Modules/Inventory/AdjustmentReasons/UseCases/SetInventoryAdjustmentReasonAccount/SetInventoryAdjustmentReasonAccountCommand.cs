using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.SetInventoryAdjustmentReasonAccount;

/// <summary>
/// ACCOUNTING-12A — configura la cuenta contable del motivo para la Company activa (contexto
/// autenticado, nunca desde el body). El maestro tenant-wide no guarda cuenta.
/// </summary>
public sealed record SetInventoryAdjustmentReasonAccountCommand(Guid ReasonId, Guid AccountingAccountId)
    : IRequest<Result<InventoryAdjustmentReasonDto>>,
        ICompanyScopedRequest;

public sealed class SetInventoryAdjustmentReasonAccountValidator
    : AbstractValidator<SetInventoryAdjustmentReasonAccountCommand>
{
    public SetInventoryAdjustmentReasonAccountValidator()
    {
        RuleFor(x => x.ReasonId).NotEmpty();
        RuleFor(x => x.AccountingAccountId).NotEmpty().WithMessage("La cuenta contable es obligatoria.");
    }
}

public sealed class SetInventoryAdjustmentReasonAccountHandler(
    IInventoryAdjustmentReasonRepository reasons,
    IInventoryAdjustmentReasonAccountRepository links,
    IAccountRepository accounts,
    ICurrentTenant tenant,
    ICurrentCompany company,
    ICurrentUser user
) : IRequestHandler<SetInventoryAdjustmentReasonAccountCommand, Result<InventoryAdjustmentReasonDto>>
{
    public async Task<Result<InventoryAdjustmentReasonDto>> Handle(
        SetInventoryAdjustmentReasonAccountCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = tenant.TenantId;
        var companyId = company.CompanyId;

        var reason = await reasons.GetByIdAsync(tenantId, cmd.ReasonId, ct);
        if (reason is null || (reason.CompanyId is { } reasonCompany && reasonCompany != companyId))
            return Result<InventoryAdjustmentReasonDto>.NotFound("Motivo no encontrado.");

        var account = await accounts.GetByIdAsync(tenantId, companyId, cmd.AccountingAccountId, ct);
        if (account is not { IsActive: true, AllowsPosting: true })
            return Result<InventoryAdjustmentReasonDto>.ValidationFailure(
                "La cuenta contable seleccionada no existe, no pertenece a esta empresa, está inactiva o no admite movimientos."
            );

        var link = await links.GetAsync(tenantId, companyId, reason.Id, ct);
        if (link is null)
            await links.AddAsync(
                InventoryAdjustmentReasonAccount.Create(tenantId, companyId, reason.Id, account.Id, user.UserId), ct);
        else
            link.ChangeAccount(account.Id, user.UserId);
        await links.SaveChangesAsync(ct);

        return Result<InventoryAdjustmentReasonDto>.Success(StockAdjustmentMapper.ToDto(reason, account.Id));
    }
}
