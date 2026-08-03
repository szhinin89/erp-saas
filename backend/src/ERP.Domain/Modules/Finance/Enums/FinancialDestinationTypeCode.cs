namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// Catálogo cerrado (2 valores estructurales, respaldado por <c>CHECK</c> a nivel de BD en Fase 2)
/// del tipo de <see cref="Entities.CompanyFinancialDestination"/> — diseño P0-02 §6.4. No es un
/// catálogo tenant-editable: los dos tipos son estructurales del propio ERP.
/// </summary>
public enum FinancialDestinationTypeCode
{
    BankAccount = 1,
    CashRegister = 2,
}
