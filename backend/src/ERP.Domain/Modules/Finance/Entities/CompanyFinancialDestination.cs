using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// Catálogo maestro persistido de destinos financieros reales de la empresa (banco o caja) — único
/// componente que resuelve el reembolso de <c>SupplierCredit</c> hacia una cuenta contable real,
/// nunca un concepto genérico "Banco/Caja" (diseño §6.4). Ocho campos estructurales inmutables
/// tras la creación (<see cref="Code"/>, <see cref="DestinationTypeCode"/>, <see cref="CurrencyCode"/>,
/// <see cref="CashRegisterId"/>, <see cref="BankInstitutionCode"/>,
/// <see cref="BankAccountIdentifierNormalized"/>, además de <c>TenantId</c>/<c>CompanyId</c>) —
/// §6.4ter: representar otra identidad económica exige crear un nuevo destino, nunca editar el
/// existente. Solo 3 métodos mutadores: <see cref="UpdateName"/>, <see cref="SetActive"/>,
/// <see cref="ChangeAccountingAccount"/>.
/// </summary>
public sealed class CompanyFinancialDestination
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int CodeMaxLen = 30;
    public const int NameMaxLen = 200;
    public const int CurrencyCodeMaxLen = 3;
    public const int BankInstitutionCodeMaxLen = 20;
    public const int BankAccountIdentifierMaxLen = 50;

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public FinancialDestinationTypeCode DestinationTypeCode { get; private set; }
    public Guid AccountingAccountId { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public Guid? CashRegisterId { get; private set; }
    public string? BankInstitutionCode { get; private set; }
    public string? BankAccountIdentifierNormalized { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CompanyFinancialDestination() { }

    public static CompanyFinancialDestination Create(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        FinancialDestinationTypeCode destinationTypeCode,
        Guid accountingAccountId,
        string currencyCode,
        Guid createdBy,
        Guid? cashRegisterId = null,
        string? bankInstitutionCode = null,
        string? bankAccountIdentifierNormalized = null
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable es obligatoria.",
                nameof(accountingAccountId)
            );
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));

        ValidateTypeSpecificFields(
            destinationTypeCode,
            cashRegisterId,
            bankInstitutionCode,
            bankAccountIdentifierNormalized
        );

        var destination = new CompanyFinancialDestination
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code.Trim(),
            Name = name.Trim(),
            DestinationTypeCode = destinationTypeCode,
            AccountingAccountId = accountingAccountId,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            CashRegisterId = cashRegisterId,
            BankInstitutionCode = OptionalCode.Normalize(bankInstitutionCode),
            BankAccountIdentifierNormalized = NormalizeBankAccountIdentifier(
                bankAccountIdentifierNormalized
            ),
            IsActive = true,
        };
        destination.SetCreated(createdBy);
        destination.RaiseDomainEvent(
            new CompanyFinancialDestinationCreatedEvent(
                tenantId,
                destination.Id,
                destination.Code,
                destination.Name,
                destination.DestinationTypeCode,
                destination.AccountingAccountId,
                destination.IsActive
            )
        );
        return destination;
    }

    /// <summary>Único campo de presentación pura editable — sin efecto en lógica ni reportes autoritativos (§6.4ter).</summary>
    public void UpdateName(string name, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        var oldName = Name;
        Name = name.Trim();
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new CompanyFinancialDestinationRenamedEvent(TenantId, Id, Code, oldName, Name)
        );
    }

    /// <summary>Habilita/deshabilita la selección del destino en operaciones nuevas — nunca afecta transacciones ya confirmadas (§6.4).</summary>
    public void SetActive(bool isActive, Guid updatedBy)
    {
        var oldIsActive = IsActive;
        IsActive = isActive;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new CompanyFinancialDestinationActiveChangedEvent(
                TenantId,
                Id,
                Code,
                oldIsActive,
                IsActive
            )
        );
    }

    /// <summary>Solo afecta operaciones futuras — cada reembolso ya confirmado congeló su propia cuenta (§6.4bis, §6.4ter).</summary>
    public void ChangeAccountingAccount(Guid accountingAccountId, Guid updatedBy)
    {
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta contable es obligatoria.",
                nameof(accountingAccountId)
            );
        var oldAccountingAccountId = AccountingAccountId;
        AccountingAccountId = accountingAccountId;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new CompanyFinancialDestinationAccountChangedEvent(
                TenantId,
                Id,
                Code,
                oldAccountingAccountId,
                AccountingAccountId
            )
        );
    }

    private static void ValidateTypeSpecificFields(
        FinancialDestinationTypeCode destinationTypeCode,
        Guid? cashRegisterId,
        string? bankInstitutionCode,
        string? bankAccountIdentifierNormalized
    )
    {
        switch (destinationTypeCode)
        {
            case FinancialDestinationTypeCode.BankAccount:
                if (string.IsNullOrWhiteSpace(bankInstitutionCode))
                    throw new ArgumentException(
                        "La institución bancaria es obligatoria para un destino tipo banco.",
                        nameof(bankInstitutionCode)
                    );
                if (string.IsNullOrWhiteSpace(bankAccountIdentifierNormalized))
                    throw new ArgumentException(
                        "El identificador de cuenta bancaria es obligatorio para un destino tipo banco.",
                        nameof(bankAccountIdentifierNormalized)
                    );
                if (cashRegisterId is not null)
                    throw new ArgumentException(
                        "Un destino tipo banco no admite una caja asociada.",
                        nameof(cashRegisterId)
                    );
                break;

            case FinancialDestinationTypeCode.CashRegister:
                if (cashRegisterId is null || cashRegisterId == Guid.Empty)
                    throw new ArgumentException(
                        "La caja es obligatoria para un destino tipo caja.",
                        nameof(cashRegisterId)
                    );
                if (
                    !string.IsNullOrWhiteSpace(bankInstitutionCode)
                    || !string.IsNullOrWhiteSpace(bankAccountIdentifierNormalized)
                )
                    throw new ArgumentException(
                        "Un destino tipo caja no admite datos bancarios.",
                        nameof(bankInstitutionCode)
                    );
                break;

            default:
                throw new ArgumentException(
                    "Tipo de destino financiero no reconocido.",
                    nameof(destinationTypeCode)
                );
        }
    }

    private static string? NormalizeBankAccountIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
}
