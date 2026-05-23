using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;

public sealed class CreateSupplierCommandHandler
    : IRequestHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository        _repo;
    private readonly IUserActivityRepository    _activity;
    private readonly ICurrentSubscriber         _tenant;
    private readonly ICurrentUser               _user;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ISupplierProfileRepository _spRepo;
    private readonly ILogger<CreateSupplierCommandHandler> _logger;

    public CreateSupplierCommandHandler(
        ISupplierRepository        repo,
        IUserActivityRepository    activity,
        ICurrentSubscriber         tenant,
        ICurrentUser               user,
        IBusinessPartnerRepository bpRepo,
        ISupplierProfileRepository spRepo,
        ILogger<CreateSupplierCommandHandler> logger)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
        _bpRepo   = bpRepo;
        _spRepo   = spRepo;
        _logger   = logger;
    }

    public async Task<Result<SupplierDto>> Handle(CreateSupplierCommand command, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var userId       = _user.UserId;

        if (await _repo.ExistsRucAsync(subscriberId, command.Ruc, null, ct))
            return Result<SupplierDto>.Failure($"Ya existe un Supplier con el RUC '{command.Ruc}' en este tenant.");

        Supplier supplier;
        try
        {
            supplier = Supplier.Create(
                subscriberId, command.PersonType, command.LegalName, command.Ruc,
                command.Email, command.Phone, command.Address, command.PaymentTerms,
                userId);
        }
        catch (ArgumentException ex)
        {
            return Result<SupplierDto>.Failure(ex.Message);
        }

        await _repo.AddAsync(supplier, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _user.Email, _user.FullName,
            module: "compras", action: "supplier.create",
            entityType: "Supplier", entityId: supplier.Id,
            description: $"{supplier.Ruc} — {supplier.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        // ── BP-4: dual-write a MasterData (best-effort, no bloquea el flujo legacy) ──
        await SyncBusinessPartnerAsync(subscriberId, userId, supplier, ct);

        return Result<SupplierDto>.Success(ToDto(supplier));
    }

    private async Task SyncBusinessPartnerAsync(
        Guid subscriberId, Guid userId, Supplier supplier, CancellationToken ct)
    {
        try
        {
            // Supplier.PersonType → TaxIdentification.Type:
            //   "Legal"   → "RUC"  (persona jurídica, 13 dígitos)
            //   "Natural" → "CI"   (persona natural, cédula)
            var idType = supplier.PersonType == Supplier.TypeLegal
                ? TaxIdentification.TypeRuc
                : TaxIdentification.TypeCi;

            var existing = await _bpRepo.GetByIdentificationAsync(idType, supplier.Ruc, ct);

            if (existing is null)
            {
                var bp = BusinessPartner.Create(
                    subscriberId, idType, supplier.Ruc,
                    supplier.LegalName, userId,
                    email: supplier.Email,
                    phone: supplier.Phone);

                await _bpRepo.AddAsync(bp, ct);

                var sp = SupplierProfile.Create(subscriberId, bp.Id, userId,
                    paymentTerms: supplier.PaymentTerms);
                await _spRepo.AddAsync(sp, ct);
                await _bpRepo.SaveChangesAsync(ct);
            }
            else if (!await _spRepo.ExistsForBusinessPartnerAsync(existing.Id, ct))
            {
                // BP existe (era solo cliente) → agregar rol proveedor
                var sp = SupplierProfile.Create(subscriberId, existing.Id, userId,
                    paymentTerms: supplier.PaymentTerms);
                await _spRepo.AddAsync(sp, ct);
                await _spRepo.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BP-4 dual-write falló para RUC {Ruc} en subscriber {Sub}. Supplier legacy guardado correctamente.",
                supplier.Ruc, subscriberId);
        }
    }

    private static SupplierDto ToDto(Supplier p) =>
        new(p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms, p.IsActive);
}
