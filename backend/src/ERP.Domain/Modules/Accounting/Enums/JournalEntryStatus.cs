namespace ERP.Domain.Modules.Accounting.Enums;

public enum JournalEntryStatus
{
    // Persistido como int. ACCOUNTING-SCHEMA-CLEANUP retiró el heredado Reversed (= 2): un asiento
    // reversado sigue Posted y su reverso lo referencia por OriginalJournalEntryId. No reutilizar 2.
    Draft = 0,
    Posted = 1,
    Discarded = 3,
}
