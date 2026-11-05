using System.Reflection;
using System.Security.Claims;
using ERP.API.Authorization;
using ERP.API.Controllers;
using ERP.Application.Access.Authorization;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests;

/// <summary>
/// SECURITY-PRECISION-POLICY-01: la escritura de la política de precisión exige
/// <c>erp.companies.update</c>; las lecturas conservan su contrato (solo sesión autenticada).
/// Evalúa las policies declaradas en el controller con el proveedor y los handlers reales de
/// <c>perm:*</c> (sin HTTP ni base de datos); el authorizer runtime se sustituye por un doble.
/// </summary>
public sealed class CompanyPrecisionPolicyPermissionContractTests
{
    private const string Update = nameof(CompanyPrecisionPolicyController.Update);
    private const string Get = nameof(CompanyPrecisionPolicyController.Get);
    private const string Metadata = nameof(CompanyPrecisionPolicyController.GetMetadata);

    public static TheoryData<string, string[], bool> Cases =>
        new()
        {
            // Escritura
            { Update, Array.Empty<string>(), false },
            { Update, new[] { SettingsPermissions.CompaniesView }, false },
            { Update, new[] { SettingsPermissions.CompaniesView, SettingsPermissions.CompanyView }, false },
            { Update, new[] { SettingsPermissions.CompaniesUpdate }, true },
            // Lecturas: contrato existente, cualquier sesión autenticada
            { Get, Array.Empty<string>(), true },
            { Get, new[] { SettingsPermissions.CompaniesView }, true },
            { Metadata, Array.Empty<string>(), true },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Endpoint_authorization_matches_its_responsibility(
        string action,
        string[] granted,
        bool expected
    )
    {
        var result = await AuthorizeAsync(action, granted);
        result.Succeeded.Should().Be(expected);
    }

    [Fact]
    public void Only_the_write_declares_a_permission_policy()
    {
        PolicyOf(Update).Should().Be($"perm:{SettingsPermissions.CompaniesUpdate}");
        PolicyOf(Get).Should().BeNull();
        PolicyOf(Metadata).Should().BeNull();
        typeof(CompanyPrecisionPolicyController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Should().ContainSingle().Which.Policy.Should().BeNull();
    }

    private static string? PolicyOf(string action) =>
        typeof(CompanyPrecisionPolicyController)
            .GetMethod(action)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault()
            ?.Policy;

    private static async Task<AuthorizationResult> AuthorizeAsync(string action, string[] granted)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, HasTenantHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IRuntimePermissionAuthorizer>(new GrantedKeysAuthorizer(granted));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Combina [Authorize] de clase y de acción como lo hace el endpoint routing.
        var authorizeData = typeof(CompanyPrecisionPolicyController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Concat(
                typeof(CompanyPrecisionPolicyController)
                    .GetMethod(action)!
                    .GetCustomAttributes<AuthorizeAttribute>()
            )
            .ToList();
        var policy = await AuthorizationPolicy.CombineAsync(
            scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>(),
            authorizeData
        );

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "User"),
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                },
                authenticationType: "Test"
            )
        );

        return await scope
            .ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, resource: null, policy!);
    }

    private sealed class GrantedKeysAuthorizer(string[] granted) : IRuntimePermissionAuthorizer
    {
        public Task<bool> IsAuthorizedAsync(
            string permissionKey,
            Guid userId,
            string role,
            CancellationToken cancellationToken
        ) => Task.FromResult(granted.Contains(permissionKey, StringComparer.Ordinal));
    }
}
