namespace ERP.Domain.Modules.Sales.ValueObjects;

public sealed record PaymentTermSnapshot
{
    public const int NameMaxLen = 120;

    public Guid   Id           { get; }
    public string Name         { get; }
    public int    Installments { get; }
    public int    DaysBetween  { get; }

    public int CreditTermDays => Installments * DaysBetween;

    private PaymentTermSnapshot(Guid id, string name, int installments, int daysBetween)
    {
        Id           = id;
        Name         = name;
        Installments = installments;
        DaysBetween  = daysBetween;
    }

    public static PaymentTermSnapshot Create(
        Guid id, string name, int installments, int daysBetween)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("La condición de pago es obligatoria.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la condición de pago es obligatorio.", nameof(name));
        if (installments < 1)
            throw new ArgumentException("Las cuotas deben ser al menos 1.", nameof(installments));
        if (daysBetween < 0)
            throw new ArgumentException("Los días entre cuotas no pueden ser negativos.", nameof(daysBetween));

        return new PaymentTermSnapshot(id, name.Trim(), installments, daysBetween);
    }
}
