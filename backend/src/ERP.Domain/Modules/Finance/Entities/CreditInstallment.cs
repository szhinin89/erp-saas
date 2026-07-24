namespace ERP.Domain.Modules.Finance.Entities;

public sealed class CreditInstallment
{
    public Guid    Id                { get; private set; }
    public Guid    CreditTermId      { get; private set; }
    public int     InstallmentNumber { get; private set; }
    public int     DaysOffset        { get; private set; }
    public decimal Percentage        { get; private set; }

    private CreditInstallment() { }

    internal static CreditInstallment Create(
        Guid creditTermId, int number, int daysOffset, decimal percentage, int totalDays)
    {
        if (number < 1)
            throw new ArgumentException("El número de cuota debe ser mayor a cero.", nameof(number));
        if (daysOffset < 0)
            throw new ArgumentException("El offset de días no puede ser negativo.", nameof(daysOffset));
        if (daysOffset > totalDays)
            throw new ArgumentException($"El offset ({daysOffset}) no puede superar el plazo total ({totalDays}).", nameof(daysOffset));
        if (percentage <= 0 || percentage > 100)
            throw new ArgumentException("El porcentaje debe estar entre 0 (exclusivo) y 100.", nameof(percentage));

        return new CreditInstallment
        {
            Id                = Guid.NewGuid(),
            CreditTermId      = creditTermId,
            InstallmentNumber = number,
            DaysOffset        = daysOffset,
            Percentage        = percentage,
        };
    }
}
