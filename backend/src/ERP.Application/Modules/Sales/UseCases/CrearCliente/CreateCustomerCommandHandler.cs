using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Sales.UseCases.CrearCliente;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository        _repo;
    private readonly IUserActivityRepository    _activity;
    private readonly ICurrentSubscriber         _tenant;
    private readonly ICurrentCompany            _company;
    private readonly ICurrentUser               _user;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICustomerProfileRepository _cpRepo;
    private readonly IDatabaseExceptionTranslator _dbExceptions;
    private readonly ISecurityMetrics _metrics;
    private readonly ILogger<CreateCustomerCommandHandler> _logger;

    public CreateCustomerCommandHandler(
        ICustomerRepository        repo,
        IUserActivityRepository    activity,
        ICurrentSubscriber         tenant,
        ICurrentCompany            company,
        ICurrentUser               user,
        IBusinessPartnerRepository bpRepo,
        ICustomerProfileRepository cpRepo,
        IDatabaseExceptionTranslator dbExceptions,
        ISecurityMetrics metrics,
        ILogger<CreateCustomerCommandHandler> logger)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _company  = company;
        _user     = user;
        _bpRepo   = bpRepo;
        _cpRepo   = cpRepo;
        _dbExceptions = dbExceptions;
        _metrics  = metrics;
        _logger   = logger;
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        if (!_tenant.IsAuthenticated || _tenant.SubscriberId == Guid.Empty)
            return Result<CustomerDto>.Failure("Subscriber no autenticado.");

        if (!_user.IsAuthenticated || _user.UserId == Guid.Empty)
            return Result<CustomerDto>.Failure("Usuario no autenticado.");

        var subscriberId = _tenant.SubscriberId;
        var userId       = _user.UserId;
        var companyId    = _company.HasCompanyContext ? _company.CompanyId : (Guid?)null;

        Customer entity;
        try
        {
            entity = Customer.Create(
                subscriberId,
                command.IdentificationType,
                command.IdentificationNumber,
                command.LegalName,
                command.TradeName,
                command.AddressLine,
                command.Phone,
                command.Email,
                command.Notes,
                userId,
                companyId: companyId);
        }
        catch (ArgumentException ex)
        {
            return Result<CustomerDto>.Failure(ex.Message);
        }

        if (await _repo.ExistsIdentificationAsync(subscriberId, entity.IdentificationType, entity.IdentificationNumber, null, ct))
            return Result<CustomerDto>.Failure("Ya existe un cliente con el mismo tipo y número de identificación.");

        if (!command.IsActive)
            entity.Disable(userId);

        await _repo.AddAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _user.Email, _user.FullName,
            module: "ventas", action: "customer.create",
            entityType: "Customer", entityId: entity.Id,
            description: $"{entity.IdentificationType} {entity.IdentificationNumber} — {entity.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        // ── BP-3: dual-write a MasterData (best-effort, no bloquea el flujo legacy) ──
        await SyncBusinessPartnerAsync(subscriberId, userId, command, ct);

        return Result<CustomerDto>.Success(new CustomerDto(
            entity.Id,
            entity.IdentificationType,
            entity.IdentificationNumber,
            entity.LegalName,
            entity.TradeName,
            entity.AddressLine,
            entity.Phone,
            entity.Email,
            entity.Notes,
            entity.IsActive));
    }

    private async Task SyncBusinessPartnerAsync(
        Guid subscriberId, Guid userId,
        CreateCustomerCommand command, CancellationToken ct)
    {
        try
        {
            var existing = await _bpRepo.GetByIdentificationAsync(
                command.IdentificationType, command.IdentificationNumber, ct);

            if (existing is null)
            {
                var bp = BusinessPartner.Create(
                    subscriberId,
                    command.IdentificationType,
                    command.IdentificationNumber,
                    command.LegalName,
                    userId,
                    command.TradeName,
                    command.Email,
                    command.Phone);

                await _bpRepo.AddAsync(bp, ct);

                var cp = CustomerProfile.Create(subscriberId, bp.Id, userId);
                await _cpRepo.AddAsync(cp, ct);
                await _bpRepo.SaveChangesAsync(ct);
            }
            else if (!await _cpRepo.ExistsForBusinessPartnerAsync(existing.Id, ct))
            {
                // BP existe (era solo proveedor) → agregar rol cliente
                var cp = CustomerProfile.Create(subscriberId, existing.Id, userId);
                await _cpRepo.AddAsync(cp, ct);
                await _cpRepo.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _metrics.RecordMasterDataDualWriteFailed(new SecurityMetricTags(
                SubscriberId: subscriberId,
                CompanyId: _company.CompanyId,
                RequestType: nameof(CreateCustomerCommand)));

            if (_dbExceptions.TryGetUniqueViolation(ex, out var violation))
            {
                UniqueViolationLogger.LogUniqueViolation(
                    _logger, "BP-3 dual-write", violation, subscriberId, _company.CompanyId);
                _metrics.RecordMasterDataSyncInconsistency(new SecurityMetricTags(
                    SubscriberId: subscriberId,
                    RequestType: "BP-3-race"));
            }
            else
            {
                _logger.LogWarning(ex,
                    "BP-3 dual-write falló para {Type} {Number} en subscriber {Sub}. Customer legacy guardado correctamente.",
                    command.IdentificationType, command.IdentificationNumber, subscriberId);
            }
        }
    }
}
