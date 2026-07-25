namespace ERP.Application.Modules.Accounting.Posting;

/// <summary><see cref="AlreadyProcessed"/> ES ÉXITO — un reintento del mismo hecho contable nunca es un error.</summary>
public enum PostingOutcomeStatus
{
    /// <summary>
    /// El JournalEntry fue preparado correctamente y quedó en staging, pendiente del commit del
    /// propietario de la transacción (ver <see cref="IPostingEngine.PostAsync"/>) — no implica
    /// que ya esté persistido físicamente en la base de datos.
    /// </summary>
    Created,
    AlreadyProcessed,
}

public sealed record PostingOutcomeDto(Guid JournalEntryId, PostingOutcomeStatus Status);
