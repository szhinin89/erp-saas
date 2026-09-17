namespace ERP.Domain.Kernel.Permissions;

/// <summary>TREASURY-BANK-ACCOUNTS-01: permisos de <c>CompanyBankAccount</c> (Tesorería → Bancos → Cuentas bancarias).</summary>
public static class TreasuryPermissions
{
    public const string BankAccountsView = "treasury.banks.accounts.view";
    public const string BankAccountsCreate = "treasury.banks.accounts.create";
    public const string BankAccountsUpdate = "treasury.banks.accounts.update";
    public const string BankAccountsManage = "treasury.banks.accounts.manage";
}
