using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ERP.API.Authorization;

/// <summary>
/// Policy dinámica: "perm:&lt;permissionKey&gt;".
/// Permite decorar endpoints con [Authorize(Policy = "perm:inventario.brands.view")].
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = policyName.Substring(Prefix.Length).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(key))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
        }

        return base.GetPolicyAsync(policyName);
    }
}

