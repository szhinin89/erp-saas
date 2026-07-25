namespace ERP.Domain.Modules.Accounting.ValueObjects;

/// <summary>
/// Código contable. Solo valida no-vacío en esta fase — sin formato Ecuador hardcodeado
/// (segmentación por nivel jerárquico, longitud fija, etc. quedan para una fase posterior
/// si el diseño del Plan de Cuentas lo requiere).
/// </summary>
public sealed class AccountCode
{
    public string Value { get; private set; } = null!;

    private AccountCode() { }

    public static AccountCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El código contable es obligatorio.", nameof(value));

        return new AccountCode { Value = value.Trim() };
    }

    public override string ToString() => Value;
}
