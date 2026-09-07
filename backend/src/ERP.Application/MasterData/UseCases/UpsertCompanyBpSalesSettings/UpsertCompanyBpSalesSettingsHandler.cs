using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSalesSettings;

public sealed class UpsertCompanyBpSalesSettingsHandler
    : IRequestHandler<UpsertCompanyBpSalesSettingsCommand, Result<CompanyBpSalesSettingsDto>>
{
    private readonly ICompanyBpSalesSettingsRepository _settingsRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPaymentTermRepository _ptRepo;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpsertCompanyBpSalesSettingsHandler(
        ICompanyBpSalesSettingsRepository settingsRepo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _settingsRepo = settingsRepo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _ctx = ctx;
        _dbEx = dbEx;
    }

    public async Task<Result<CompanyBpSalesSettingsDto>> Handle(
        UpsertCompanyBpSalesSettingsCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (!_ctx.HasCompany)
            return Result<CompanyBpSalesSettingsDto>.ValidationFailure(
                "Contexto de empresa no establecido."
            );

        var bp = await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, cancellationToken);
        if (bp is null)
            return Result<CompanyBpSalesSettingsDto>.NotFound("Cliente no encontrado.");
        if (!bp.IsActive)
            return Result<CompanyBpSalesSettingsDto>.ValidationFailure(
                "El cliente se encuentra inactivo."
            );

        var role = await _roleRepo.GetByTypeAsync(
            cmd.BusinessPartnerId,
            RoleType.Customer,
            cancellationToken
        );
        if (role is null || !role.IsActive)
            return Result<CompanyBpSalesSettingsDto>.ValidationFailure(
                "El tercero no tiene rol de Cliente activo."
            );

        if (cmd.PaymentTermId is { } ptId)
        {
            var pt = await _ptRepo.GetByIdAsync(_ctx.TenantId, ptId, cancellationToken);
            if (pt is null)
                return Result<CompanyBpSalesSettingsDto>.ValidationFailure(
                    "La condición de pago no existe."
                );
            if (!pt.IsActive)
                return Result<CompanyBpSalesSettingsDto>.ValidationFailure(
                    "La condición de pago se encuentra inactiva."
                );
        }

        var existing = await _settingsRepo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );

        CompanyBpSalesSettings settings;
        if (existing is not null)
        {
            existing.SetPaymentTerm(cmd.PaymentTermId, _ctx.UserId);
            settings = existing;
        }
        else
        {
            try
            {
                settings = CompanyBpSalesSettings.Create(
                    _ctx.TenantId,
                    _ctx.CompanyId,
                    cmd.BusinessPartnerId,
                    cmd.PaymentTermId,
                    _ctx.UserId
                );
            }
            catch (ArgumentException ex)
            {
                return Result<CompanyBpSalesSettingsDto>.ValidationFailure(ex.Message);
            }
            await _settingsRepo.AddAsync(settings, cancellationToken);
        }

        try
        {
            await _settingsRepo.SaveChangesAsync(cancellationToken);
            return Result<CompanyBpSalesSettingsDto>.Success(
                CompanyBpSalesSettingsDto.From(settings)
            );
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<CompanyBpSalesSettingsDto>.Conflict(
                "Ya existe una configuración de compras para este cliente en esta empresa (race condition)."
            );
        }
    }
}
