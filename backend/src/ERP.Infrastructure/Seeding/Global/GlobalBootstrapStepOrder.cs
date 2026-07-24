namespace ERP.Infrastructure.Seeding.Global;

/// <summary>
/// Fuente única de los valores de <see cref="IGlobalBootstrapStep.Order"/>. Misma convención que
/// <c>CompanyBootstrapStepOrder</c>: incrementos de 10 para insertar un step nuevo sin renumerar.
/// </summary>
public static class GlobalBootstrapStepOrder
{
    /// <summary>Navegación (ui_nav_groups/ui_nav_items) sincronizada desde KernelRegistry.</summary>
    public const int Navigation = 10;

    /// <summary>Scripts de datos globales inmutables (geografía, país) — no bloqueante si falla.</summary>
    public const int InstallData = 20;
}
