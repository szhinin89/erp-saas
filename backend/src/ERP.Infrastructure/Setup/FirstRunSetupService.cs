using ERP.Application.Setup;
using ERP.Domain.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Setup;

/// <summary>
/// On startup, while the system is uninitialized, issues a fresh cryptographically-secure
/// setup token (32 random bytes, hex-encoded), persists only its SHA-256 hash with an
/// expiry, and prints the raw token to the console — the single time it is ever exposed.
///
/// Never reads the token from appsettings/env vars — it is generated, never configured.
/// </summary>
public sealed class FirstRunSetupService : IFirstRunSetupService
{
    private const int DefaultExpiryMinutes = 15;

    private readonly ISystemSetupRepository _repo;
    private readonly IConfiguration         _config;
    private readonly ILogger<FirstRunSetupService> _logger;

    public FirstRunSetupService(ISystemSetupRepository repo, IConfiguration config, ILogger<FirstRunSetupService> logger)
    {
        _repo   = repo;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureSetupTokenAsync(CancellationToken cancellationToken = default)
    {
        var state = await _repo.GetAsync(cancellationToken);
        if (state is null)
        {
            state = SystemSetupState.CreateNew();
            await _repo.AddAsync(state, cancellationToken);
        }

        if (state.IsInitialized)
            return;

        var now = DateTime.UtcNow;
        if (state.HasActiveSetupToken(now))
            return;

        var expiryMinutes = _config.GetValue("Setup:TokenExpiryMinutes", DefaultExpiryMinutes);
        var rawToken      = SetupTokenCrypto.CreateRawToken();
        var expiryUtc     = now.AddMinutes(expiryMinutes);

        state.IssueSetupToken(SetupTokenCrypto.Hash(rawToken), expiryUtc);
        await _repo.SaveChangesAsync(cancellationToken);

        PrintBanner(rawToken, expiryMinutes);
    }

    private void PrintBanner(string rawToken, int expiryMinutes)
    {
        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("FIRST RUN DETECTADO");
        Console.WriteLine("==================================================");
        Console.WriteLine();
        Console.WriteLine("TOKEN DE INSTALACIÓN:");
        Console.WriteLine();
        Console.WriteLine(rawToken);
        Console.WriteLine();
        Console.WriteLine($"Expira en {expiryMinutes} minutos.");
        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine();

        _logger.LogWarning(
            "First-run setup token generado — expira en {ExpiryMinutes} minutos. Úselo de inmediato en POST /api/v1/setup/admin.",
            expiryMinutes);
    }
}
