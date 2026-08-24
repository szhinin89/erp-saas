namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo InitialLoad — Carga Inicial (INITIAL-LOAD-ARCH-01).
/// </summary>
public static class InitialLoadPermissions
{
    public const string View = "initialload.batches.view";
    public const string Create = "initialload.batches.create";
    public const string Confirm = "initialload.batches.confirm";

    public static IReadOnlyList<string> All { get; } = [View, Create, Confirm];
}
