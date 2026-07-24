using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Entities;

public sealed class CreditTerm : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 120;

    public string         Code      { get; private set; } = null!;
    public string         Name      { get; private set; } = null!;
    public CreditTermMode Mode      { get; private set; }
    public int            TotalDays { get; private set; }

    private readonly List<CreditInstallment> _installments = new();
    public IReadOnlyList<CreditInstallment> Installments => _installments.AsReadOnly();

    private CreditTerm() { }

    public static CreditTerm Create(
        Guid           tenantId,
        Guid           companyId,
        string         code,
        string         name,
        CreditTermMode mode,
        int            totalDays,
        Guid           createdBy,
        IEnumerable<(int Number, int DaysOffset, decimal Percentage)>? installments = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (totalDays < 0)
            throw new ArgumentException("El plazo total no puede ser negativo.", nameof(totalDays));

        var ct = new CreditTerm
        {
            Id        = Guid.NewGuid(),
            TenantId  = tenantId,
            CompanyId = companyId,
            Code      = code.Trim().ToUpperInvariant(),
            Name      = name.Trim(),
            Mode      = mode,
            TotalDays = totalDays,
        };
        ct.SetCreated(createdBy);
        ct.ApplyInstallments(installments);
        return ct;
    }

    public void Update(
        string         name,
        CreditTermMode mode,
        int            totalDays,
        Guid           updatedBy,
        IEnumerable<(int Number, int DaysOffset, decimal Percentage)>? installments = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (totalDays < 0)
            throw new ArgumentException("El plazo total no puede ser negativo.", nameof(totalDays));

        Name      = name.Trim();
        Mode      = mode;
        TotalDays = totalDays;
        SetUpdated(updatedBy);
        ApplyInstallments(installments);
    }

    private void ApplyInstallments(
        IEnumerable<(int Number, int DaysOffset, decimal Percentage)>? installments)
    {
        _installments.Clear();

        var list = installments?.ToList();

        if (list is null || list.Count == 0)
        {
            if (Mode == CreditTermMode.FinancialStrict)
                throw new InvalidOperationException(
                    "El modo FinancialStrict requiere al menos una cuota.");

            _installments.Add(
                CreditInstallment.Create(Id, 1, TotalDays, 100.00m, TotalDays));
            return;
        }

        foreach (var (number, days, pct) in list.OrderBy(i => i.Number))
            _installments.Add(CreditInstallment.Create(Id, number, days, pct, TotalDays));

        var sum = _installments.Sum(i => i.Percentage);
        if (sum != 100.00m)
            throw new InvalidOperationException(
                $"La suma de porcentajes de cuotas debe ser exactamente 100%. Actual: {sum}%.");
    }
}
