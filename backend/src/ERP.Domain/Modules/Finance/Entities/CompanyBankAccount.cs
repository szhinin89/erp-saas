using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// TREASURY-BANK-ACCOUNTS-01: cuenta bancaria de empresa — referencia al catálogo maestro
/// <see cref="ERP.Domain.MasterData.Entities.Bank"/> (nunca banco como texto libre) más los datos
/// propios de la cuenta (tipo, número, alias) y la cuenta contable a la que postea. Alcance
/// estricto: CRUD básico + activar/desactivar — sin posting, sin caja, sin conciliación, sin
/// movimientos bancarios (eso pertenece a un módulo bancario completo futuro).
/// </summary>
public sealed class CompanyBankAccount : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int AccountNumberMaxLen = 50;
    public const int DisplayNameMaxLen = 200;

    public Guid CompanyId { get; private set; }
    public Guid BankId { get; private set; }
    public BankAccountType AccountType { get; private set; }
    public string AccountNumber { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public Guid AccountingAccountId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CompanyBankAccount() { }

    public static CompanyBankAccount Create(
        Guid tenantId,
        Guid companyId,
        Guid bankId,
        BankAccountType accountType,
        string accountNumber,
        string displayName,
        Guid accountingAccountId,
        Guid createdBy
    )
    {
        var (normalizedAccountNumber, normalizedDisplayName) = Validate(
            bankId,
            accountNumber,
            displayName,
            accountingAccountId
        );

        var account = new CompanyBankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BankId = bankId,
            AccountType = accountType,
            AccountNumber = normalizedAccountNumber,
            DisplayName = normalizedDisplayName,
            AccountingAccountId = accountingAccountId,
            IsActive = true,
        };
        account.SetCreated(createdBy);
        return account;
    }

    /// <summary>Modifica alias visible y cuenta contable — Banco/Tipo/Número son inmutables tras la creación (identifican la cuenta real).</summary>
    public void Update(string displayName, Guid accountingAccountId, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("El alias/nombre visible es obligatorio.", nameof(displayName));
        if (displayName.Trim().Length > DisplayNameMaxLen)
            throw new ArgumentException(
                $"El alias/nombre visible no puede superar {DisplayNameMaxLen} caracteres.",
                nameof(displayName)
            );
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountingAccountId));

        DisplayName = displayName.Trim();
        AccountingAccountId = accountingAccountId;
        SetUpdated(updatedBy);
    }

    public void Enable(Guid updatedBy)
    {
        IsActive = true;
        SetUpdated(updatedBy);
    }

    public void Disable(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    private static (string AccountNumber, string DisplayName) Validate(
        Guid bankId,
        string accountNumber,
        string displayName,
        Guid accountingAccountId
    )
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("El banco es obligatorio.", nameof(bankId));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("El número de cuenta es obligatorio.", nameof(accountNumber));
        var normalizedAccountNumber = accountNumber.Trim();
        if (normalizedAccountNumber.Length > AccountNumberMaxLen)
            throw new ArgumentException(
                $"El número de cuenta no puede superar {AccountNumberMaxLen} caracteres.",
                nameof(accountNumber)
            );
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("El alias/nombre visible es obligatorio.", nameof(displayName));
        var normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length > DisplayNameMaxLen)
            throw new ArgumentException(
                $"El alias/nombre visible no puede superar {DisplayNameMaxLen} caracteres.",
                nameof(displayName)
            );
        if (accountingAccountId == Guid.Empty)
            throw new ArgumentException("La cuenta contable es obligatoria.", nameof(accountingAccountId));

        return (normalizedAccountNumber, normalizedDisplayName);
    }
}
