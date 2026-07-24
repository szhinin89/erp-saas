using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Perfiles de acceso por defecto del tenant. Delega en <see cref="IDefaultProfileSeeder"/>
/// (contrato público sin cambios, sigue siendo invocable/testeable de forma independiente) —
/// este step solo lo integra al único flujo oficial de bootstrap. Se ejecuta al final porque no
/// depende de ningún dato creado por los demás steps.
/// </summary>
public sealed class AccessBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Access;

    private readonly IDefaultProfileSeeder _profileSeeder;

    public AccessBootstrapStep(IDefaultProfileSeeder profileSeeder)
    {
        _profileSeeder = profileSeeder;
    }

    public Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
        => _profileSeeder.SeedForTenantAsync(context.TenantId, context.ActorId, cancellationToken);
}
