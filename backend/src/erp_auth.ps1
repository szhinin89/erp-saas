# ZH Technologies - Autenticacion JWT
# Ejecutar desde: C:\ProyectCursor\erp-saas\backend\src

$D = "ERP.Domain"
$A = "ERP.Application"
$I = "ERP.Infrastructure"
$API = "ERP.API"

Write-Host "=== Auth JWT Setup ===" -ForegroundColor Cyan

# Paquetes necesarios
Write-Host "[1/5] Instalando paquetes..." -ForegroundColor Yellow
dotnet add "$I\ERP.Infrastructure.csproj" package BCrypt.Net-Next
dotnet add "$API\ERP.API.csproj" package System.IdentityModel.Tokens.Jwt
Write-Host "      OK" -ForegroundColor Green

# Carpetas
Write-Host "[2/5] Creando carpetas..." -ForegroundColor Yellow
@(
    "$D\Modules\Auth\Entities",
    "$D\Modules\Auth\Interfaces",
    "$D\Modules\Auth\ValueObjects",
    "$A\Modules\Auth\UseCases\Register",
    "$A\Modules\Auth\UseCases\Login",
    "$A\Modules\Auth\DTOs",
    "$I\Persistence\Configurations",
    "$I\Services"
) | ForEach-Object { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
Write-Host "      OK" -ForegroundColor Green

Write-Host "[3/5] Generando Domain - Auth..." -ForegroundColor Yellow

# ── User Entity ───────────────────────────────────────────────────────────────
Set-Content "$D\Modules\Auth\Entities\User.cs" @'
using ERP.Domain.Common;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Domain.Auth.Entities;

public class User : AuditableEntity
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private User() { }

    public static User Create(
        Guid tenantId,
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        string role,
        Guid createdBy)
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            FirstName    = firstName,
            LastName     = lastName,
            Email        = new Email(email),
            PasswordHash = passwordHash,
            Role         = role,
            IsActive     = true
        };
        user.SetCreated(createdBy);
        return user;
    }

    public string FullName => $"{FirstName} {LastName}";

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}
'@

# ── Email ValueObject ─────────────────────────────────────────────────────────
Set-Content "$D\Modules\Auth\ValueObjects\Email.cs" @'
namespace ERP.Domain.Auth.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El email no puede estar vacio.");
        if (!value.Contains('@'))
            throw new ArgumentException("El email no es valido.");
        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
'@

# ── IUserRepository ───────────────────────────────────────────────────────────
Set-Content "$D\Modules\Auth\Interfaces\IUserRepository.cs" @'
using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
'@

# ── IJwtService ───────────────────────────────────────────────────────────────
Set-Content "$D\Modules\Auth\Interfaces\IJwtService.cs" @'
using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}
'@

Write-Host "[4/5] Generando Application + Infrastructure + API..." -ForegroundColor Yellow

# ── DTOs ──────────────────────────────────────────────────────────────────────
Set-Content "$A\Modules\Auth\DTOs\AuthDto.cs" @'
namespace ERP.Application.Auth.DTOs;

public record RegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid TenantId
);

public record LoginDto(
    string Email,
    string Password,
    Guid TenantId
);

public record AuthResponseDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    Guid TenantId,
    string Token
);
'@

# ── Register Use Case ─────────────────────────────────────────────────────────
Set-Content "$A\Modules\Auth\UseCases\Register\RegisterCommand.cs" @'
namespace ERP.Application.Auth.UseCases.Register;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid TenantId,
    string Role = "User"
);
'@

Set-Content "$A\Modules\Auth\UseCases\Register\RegisterHandler.cs" @'
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.Register;

public class RegisterHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IJwtService _jwtService;

    public RegisterHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IJwtService jwtService)
    {
        _userRepository   = userRepository;
        _tenantRepository = tenantRepository;
        _jwtService       = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        RegisterCommand command,
        CancellationToken ct = default)
    {
        var tenantExists = await _tenantRepository.ExistsAsync(command.TenantId, ct);
        if (!tenantExists)
            return Result<AuthResponseDto>.Failure("El tenant no existe.");

        var emailExists = await _userRepository.ExistsAsync(command.Email, command.TenantId, ct);
        if (emailExists)
            return Result<AuthResponseDto>.Failure("Ya existe un usuario con ese email.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        var user = User.Create(
            command.TenantId,
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHash,
            command.Role,
            Guid.Empty);

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        var token = _jwtService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            user.TenantId,
            token));
    }
}
'@

# ── Login Use Case ────────────────────────────────────────────────────────────
Set-Content "$A\Modules\Auth\UseCases\Login\LoginCommand.cs" @'
namespace ERP.Application.Auth.UseCases.Login;

public record LoginCommand(
    string Email,
    string Password,
    Guid TenantId
);
'@

Set-Content "$A\Modules\Auth\UseCases\Login\LoginHandler.cs" @'
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Application.Auth.UseCases.Login;

public class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public LoginHandler(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService     = jwtService;
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(command.Email, command.TenantId, ct);
        if (user is null)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure("Usuario inactivo.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
            return Result<AuthResponseDto>.Failure("Credenciales invalidas.");

        var token = _jwtService.GenerateToken(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email.Value,
            user.Role,
            user.TenantId,
            token));
    }
}
'@

# ── JwtService ────────────────────────────────────────────────────────────────
Set-Content "$I\Services\JwtService.cs" @'
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secretKey  = _configuration["Jwt:SecretKey"]!;
        var issuer     = _configuration["Jwt:Issuer"]!;
        var audience   = _configuration["Jwt:Audience"]!;
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("full_name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
'@

# ── UserRepository ────────────────────────────────────────────────────────────
Set-Content "$I\Persistence\Repositories\UserRepository.cs" @'
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ErpDbContext _context;

    public UserRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Email.Value == email, ct);

    public async Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default)
        => await _context.Users.AnyAsync(u => u.Email.Value == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
'@

# ── UserConfiguration ─────────────────────────────────────────────────────────
Set-Content "$I\Persistence\Configurations\UserConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired()
            .HasConversion(e => e.Value, v => new Email(v));
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasDatabaseName("ix_users_tenant_email");

        builder.HasQueryFilter(u => u.TenantId == EF.Property<Guid>(u, "TenantId"));
    }
}
'@

# ── Agregar Users al DbContext ────────────────────────────────────────────────
$dbContextPath = "$I\Persistence\ErpDbContext.cs"
$dbContent = Get-Content $dbContextPath -Raw
$dbContent = $dbContent -replace "using ERP.Domain.Tenants.Entities;", "using ERP.Domain.Auth.Entities;`nusing ERP.Domain.Tenants.Entities;"
$dbContent = $dbContent -replace "public DbSet<Tenant> Tenants", "public DbSet<User> Users => Set<User>();`n    public DbSet<Tenant> Tenants"
$dbContent = $dbContent -replace "modelBuilder.Entity<Product\(\)>`n            .HasQueryFilter", "modelBuilder.Entity<User>()`n            .HasQueryFilter(e => e.TenantId == tenantId);`n`n        modelBuilder.Entity<Product>()`n            .HasQueryFilter"
Set-Content $dbContextPath $dbContent

# ── Registrar en DependencyInjection ─────────────────────────────────────────
$diPath = "$I\DependencyInjection.cs"
$diContent = Get-Content $diPath -Raw
$diContent = $diContent -replace "using ERP.Domain.Tenants.Interfaces;", "using ERP.Domain.Auth.Interfaces;`nusing ERP.Domain.Tenants.Interfaces;"
$diContent = $diContent -replace "using ERP.Infrastructure.Persistence.Repositories;", "using ERP.Infrastructure.Persistence.Repositories;`nusing ERP.Infrastructure.Services;"
$diContent = $diContent -replace "services.AddScoped<ITenantRepository, TenantRepository>\(\);", "services.AddScoped<IUserRepository, UserRepository>();`n        services.AddScoped<IJwtService, JwtService>();`n        services.AddScoped<ITenantRepository, TenantRepository>();"
Set-Content $diPath $diContent

# ── AuthController ────────────────────────────────────────────────────────────
Set-Content "$API\Controllers\AuthController.cs" @'
using Microsoft.AspNetCore.Mvc;
using ERP.Application.Auth.UseCases.Register;
using ERP.Application.Auth.UseCases.Login;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly RegisterHandler _registerHandler;
    private readonly LoginHandler    _loginHandler;

    public AuthController(RegisterHandler registerHandler, LoginHandler loginHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler    = loginHandler;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error });
    }
}
'@

# ── Registrar handlers en Program.cs ─────────────────────────────────────────
$programPath = "$API\Program.cs"
$programContent = Get-Content $programPath -Raw
$programContent = $programContent -replace "// Handlers", "// Handlers`nbuilder.Services.AddScoped<ERP.Application.Auth.UseCases.Register.RegisterHandler>();`nbuilder.Services.AddScoped<ERP.Application.Auth.UseCases.Login.LoginHandler>();"
Set-Content $programPath $programContent

# ── Build ─────────────────────────────────────────────────────────────────────
Write-Host "[5/5] Compilando..." -ForegroundColor Yellow
dotnet build ERP.slnx

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Auth JWT listo" -ForegroundColor Green
Write-Host ""
Write-Host " Migracion nueva tabla users:" -ForegroundColor Yellow
Write-Host " dotnet ef migrations add AddUsersTable --project ERP.Infrastructure --startup-project ERP.API" -ForegroundColor Cyan
Write-Host " dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
