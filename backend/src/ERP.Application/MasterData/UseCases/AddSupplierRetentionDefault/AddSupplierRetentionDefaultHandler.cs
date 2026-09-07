using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;

public sealed class AddSupplierRetentionDefaultHandler
    : IRequestHandler<AddSupplierRetentionDefaultCommand, Result<SupplierRetentionDefaultDto>>
{
    private readonly ISupplierRetentionDefaultRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly ISriCatalogLookupRepository _catalogRepo;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public AddSupplierRetentionDefaultHandler(
        ISupplierRetentionDefaultRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        ISriCatalogLookupRepository catalogRepo,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _catalogRepo = catalogRepo;
        _ctx = ctx;
        _dbEx = dbEx;
    }

    public async Task<Result<SupplierRetentionDefaultDto>> Handle(
        AddSupplierRetentionDefaultCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasCompany)
            return Result<SupplierRetentionDefaultDto>.ValidationFailure(
                "Contexto de empresa no establecido."
            );

        var bp = await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, cancellationToken);
        if (bp is null)
            return Result<SupplierRetentionDefaultDto>.NotFound("Proveedor no encontrado.");
        if (!bp.IsActive)
            return Result<SupplierRetentionDefaultDto>.ValidationFailure(
                "El proveedor se encuentra inactivo."
            );

        var role = await _roleRepo.GetByTypeAsync(
            cmd.BusinessPartnerId,
            RoleType.Supplier,
            cancellationToken
        );
        if (role is null || !role.IsActive)
            return Result<SupplierRetentionDefaultDto>.ValidationFailure(
                "El tercero no tiene rol de Proveedor activo."
            );

        var existing = await _repo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );
        if (existing.Any(x => x.SriRetentionCodeId == cmd.SriRetentionCodeId))
            return Result<SupplierRetentionDefaultDto>.Conflict(
                "Ya existe una retención predeterminada con este código para este proveedor en esta empresa."
            );

        var nextOrder = existing.Count == 0 ? 0 : existing.Max(x => x.DisplayOrder) + 1;

        SupplierRetentionDefault entry;
        try
        {
            entry = SupplierRetentionDefault.Create(
                _ctx.TenantId,
                _ctx.CompanyId,
                cmd.BusinessPartnerId,
                cmd.SriRetentionCodeId,
                nextOrder,
                _ctx.UserId
            );
        }
        catch (ArgumentException ex)
        {
            return Result<SupplierRetentionDefaultDto>.ValidationFailure(ex.Message);
        }

        await _repo.AddAsync(entry, cancellationToken);

        try
        {
            await _repo.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<SupplierRetentionDefaultDto>.Conflict(
                "Ya existe una retención predeterminada con este código para este proveedor en esta empresa (race condition)."
            );
        }

        var catalogCode = await _catalogRepo.GetRetentionCodeByIdAsync(
            cmd.SriRetentionCodeId,
            cancellationToken
        );
        return Result<SupplierRetentionDefaultDto>.Success(
            SupplierRetentionDefaultDto.From(entry, catalogCode)
        );
    }
}
