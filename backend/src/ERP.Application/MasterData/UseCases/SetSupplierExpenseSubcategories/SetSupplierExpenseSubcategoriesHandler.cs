using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;

public sealed class SetSupplierExpenseSubcategoriesHandler
    : IRequestHandler<SetSupplierExpenseSubcategoriesCommand, Result<SupplierExpenseSubcategoriesDto>>
{
    private readonly ISupplierExpenseSubcategoryRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public SetSupplierExpenseSubcategoriesHandler(
        ISupplierExpenseSubcategoryRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ctx = ctx;
        _dbEx = dbEx;
    }

    public async Task<Result<SupplierExpenseSubcategoriesDto>> Handle(
        SetSupplierExpenseSubcategoriesCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasCompany)
            return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                "Contexto de empresa no establecido."
            );

        var bp = await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, cancellationToken);
        if (bp is null)
            return Result<SupplierExpenseSubcategoriesDto>.NotFound("Proveedor no encontrado.");
        if (!bp.IsActive)
            return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                "El proveedor se encuentra inactivo."
            );
        var role = await _roleRepo.GetByTypeAsync(
            cmd.BusinessPartnerId,
            RoleType.Supplier,
            cancellationToken
        );
        if (role is null || !role.IsActive)
            return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                "El tercero no tiene rol de Proveedor activo."
            );

        var existing = await _repo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );
        var existingBySubcategory = existing.ToDictionary(x => x.ExpenseSubcategoryId);
        var desired = cmd.ExpenseSubcategoryIds.ToHashSet();

        // Los ids pedidos deben ser subcategorías visibles en la empresa activa (el filtro de
        // empresa oculta las de otra: no existe para esta empresa). Solo exige nodo activo a lo que
        // se crea o reactiva; retirar una asignación no depende del estado del catálogo.
        var infos = (
            await _repo.GetSubcategoryInfosAsync(desired.ToList(), cancellationToken)
        ).ToDictionary(i => i.Id);
        foreach (var id in cmd.ExpenseSubcategoryIds)
        {
            if (!infos.TryGetValue(id, out var info))
                return Result<SupplierExpenseSubcategoriesDto>.NotFound(
                    "La subcategoría de gasto indicada no existe."
                );
            if (!info.IsSubcategory)
                return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                    $"'{info.Name}' no es una subcategoría de gasto."
                );
            var alreadyActive =
                existingBySubcategory.TryGetValue(id, out var current) && current.IsActive;
            if (!alreadyActive && !info.IsActive)
                return Result<SupplierExpenseSubcategoriesDto>.ValidationFailure(
                    $"La subcategoría '{info.Name}' está inactiva."
                );
        }

        // Reemplazo atómico: una sola unidad de trabajo. Solo se invoca Enable/Disable cuando el
        // estado cambia (lanzan si ya coincide) → idempotente y sin eventos de auditoría de no-ops.
        foreach (var id in desired)
        {
            if (!existingBySubcategory.TryGetValue(id, out var row))
            {
                await _repo.AddAsync(
                    SupplierExpenseSubcategory.Create(
                        _ctx.TenantId,
                        _ctx.CompanyId,
                        cmd.BusinessPartnerId,
                        id,
                        _ctx.UserId
                    ),
                    cancellationToken
                );
            }
            else if (!row.IsActive)
            {
                row.Enable(_ctx.UserId);
            }
        }
        foreach (var row in existing.Where(x => x.IsActive && !desired.Contains(x.ExpenseSubcategoryId)))
            row.Disable(_ctx.UserId);

        try
        {
            await _repo.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<SupplierExpenseSubcategoriesDto>.Conflict(
                "Otra solicitud modificó simultáneamente las subcategorías de este proveedor. Reintente."
            );
        }

        var updated = await _repo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );
        var updatedInfos = await _repo.GetSubcategoryInfosAsync(
            updated.Select(x => x.ExpenseSubcategoryId).ToList(),
            cancellationToken
        );
        return Result<SupplierExpenseSubcategoriesDto>.Success(
            SupplierExpenseSubcategoriesDto.Build(updated, updatedInfos)
        );
    }
}
