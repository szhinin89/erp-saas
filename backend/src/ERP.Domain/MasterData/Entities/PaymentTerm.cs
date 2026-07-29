using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

public sealed class PaymentTerm : MasterEntity, ITenantScopedEntity
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 120;
    public const int MaxInstallments = 60;

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int Installments { get; private set; }
    public int DaysBetweenInstallments { get; private set; }

    private PaymentTerm() { }

    public static PaymentTerm Create(
        Guid tenantId,
        string code,
        string name,
        int installments,
        int daysBetweenInstallments,
        Guid createdBy)
    {
        Validate(code, name, installments, daysBetweenInstallments);

        var pt = new PaymentTerm
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Installments = installments,
            DaysBetweenInstallments = daysBetweenInstallments,
        };
        pt.SetCreated(createdBy);
        return pt;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Contado), ver política de Bootstrap en <c>CLAUDE.md</c>.
    /// <see cref="Update"/> permanece abierto.
    /// </summary>
    public static PaymentTerm CreateSystemSeeded(
        Guid tenantId,
        string code,
        string name,
        int installments,
        int daysBetweenInstallments,
        Guid createdBy)
    {
        var pt = Create(tenantId, code, name, installments, daysBetweenInstallments, createdBy);
        pt.MarkAsSystemSeeded();
        return pt;
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La condición de pago", "deshabilitarse");

        base.Disable(updatedBy);
    }

    public void Update(string name, int installments, int daysBetweenInstallments, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (installments < 1 || installments > MaxInstallments)
            throw new ArgumentException($"Las cuotas deben estar entre 1 y {MaxInstallments}.", nameof(installments));
        if (daysBetweenInstallments < 0)
            throw new ArgumentException("Los días entre cuotas no pueden ser negativos.", nameof(daysBetweenInstallments));

        Name = name.Trim();
        Installments = installments;
        DaysBetweenInstallments = daysBetweenInstallments;
        SetUpdated(updatedBy);
    }

    public int TotalDays => Installments * DaysBetweenInstallments;

    public string Summary => Installments == 1 && DaysBetweenInstallments == 0
        ? "Contado"
        : Installments == 1
            ? $"{DaysBetweenInstallments} días"
            : $"{Installments}x{DaysBetweenInstallments}";

    private static void Validate(string code, string name, int installments, int daysBetweenInstallments)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Trim().Length > MaxCodeLength)
            throw new ArgumentException($"El código no puede superar {MaxCodeLength} caracteres.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"El nombre no puede superar {MaxNameLength} caracteres.", nameof(name));
        if (installments < 1 || installments > MaxInstallments)
            throw new ArgumentException($"Las cuotas deben estar entre 1 y {MaxInstallments}.", nameof(installments));
        if (daysBetweenInstallments < 0)
            throw new ArgumentException("Los días entre cuotas no pueden ser negativos.", nameof(daysBetweenInstallments));
    }
}
