using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.UseCases.RegisterTenantWithAdmin;

public class RegisterTenantWithAdminHandler
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;

    public RegisterTenantWithAdminHandler(
        ITenantRepository tenantRepository,
        IAccessRepository accessRepository,
        IAccessTokenService tokenService)
    {
        _tenantRepository = tenantRepository;
        _accessRepository = accessRepository;
        _tokenService = tokenService;
    }

    public async Task<Result<SessionResponseDto>> HandleAsync(RegisterTenantWithAdminCommand command, CancellationToken ct = default)
    {
        var slug = command.TenantSlug.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            return Result<SessionResponseDto>.Failure("Slug inválido.");

        var existingTenant = await _tenantRepository.GetBySlugAsync(slug, ct);
        if (existingTenant is not null)
            return Result<SessionResponseDto>.Failure("El slug ya está en uso.");

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await _accessRepository.AnyUserWithEmailAsync(email, ct))
            return Result<SessionResponseDto>.Failure("El email ya está registrado en el sistema.");

        var tenant = Tenant.Create(
            command.TenantName,
            slug,
            createdBy: Guid.Empty,
            passwordResetMode: command.PasswordResetMode,
            ruc: command.Ruc,
            shortName: command.ShortName,
            tradeName: command.TradeName,
            dinardap: command.Dinardap,
            logoUrl: command.LogoUrl,
            displayOrder: command.DisplayOrder,
            priority: command.Priority);
        await _tenantRepository.AddAsync(tenant, ct);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.AdminPassword);
        var identityUser = IdentityUser.Create(
            firstName: command.AdminFirstName,
            lastName: command.AdminLastName,
            email: email,
            passwordHash: passwordHash,
            createdBy: Guid.Empty);
        await _accessRepository.AddUserAsync(identityUser, ct);

        var membership = Membership.Create(
            tenantId: tenant.Id,
            identityUserId: identityUser.Id,
            role: "Admin",
            profileId: null,
            createdBy: Guid.Empty);
        await _accessRepository.AddMembershipAsync(membership, ct);

        await _tenantRepository.SaveChangesAsync(ct);
        await _accessRepository.SaveChangesAsync(ct);

        var sessionToken = _tokenService.GenerateSessionToken(identityUser, tenant.Id, "Admin");
        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: identityUser.Id,
            FullName: identityUser.FullName,
            Email: identityUser.Email.Value,
            TenantId: tenant.Id,
            Role: "Admin",
            Token: sessionToken));
    }
}

