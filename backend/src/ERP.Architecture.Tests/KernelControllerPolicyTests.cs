using System.Reflection;
using ERP.Domain.Kernel;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Architecture.Tests;

/// <summary>
/// Cierra "no orphan permissions" del lado backend: toda policy <c>perm:X</c> usada en
/// <c>[Authorize]</c> sobre controllers de ERP.API debe existir en
/// <see cref="KernelRegistry.Permissions"/> — única fuente de verdad de permisos.
/// </summary>
public sealed class KernelControllerPolicyTests
{
    private const string PermPrefix = "perm:";

    [Fact]
    public void Controller_perm_policies_exist_in_KernelRegistry_Permissions()
    {
        var allowed = new HashSet<string>(KernelRegistry.Permissions, StringComparer.Ordinal);

        var controllerAssembly = ResolveControllersAssembly();

        var controllerTypes = controllerAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        controllerTypes.Should().NotBeEmpty();

        var orphanPolicies = new List<string>();

        foreach (var type in controllerTypes)
        {
            var attributes = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));

            foreach (var attr in attributes)
            {
                if (attr.Policy is not { } policy || !policy.StartsWith(PermPrefix, StringComparison.Ordinal))
                    continue;

                var permissionKey = policy[PermPrefix.Length..];
                if (!allowed.Contains(permissionKey))
                    orphanPolicies.Add($"{type.Name}: {policy}");
            }
        }

        orphanPolicies.Should().BeEmpty("toda policy 'perm:X' debe corresponder a una constante en ERP.Domain.Kernel.Permissions.*");
    }

    private static Assembly ResolveControllersAssembly()
        => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ERP.API")
            ?? Assembly.Load("ERP.API");
}
